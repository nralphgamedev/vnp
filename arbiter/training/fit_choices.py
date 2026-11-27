"""
Fits a decision's term weights to someone's recorded choices: Arbiter's first training tool.

A choice is a list of options, each described by the values of the terms that score it, and which option was
taken. Fitting finds the weights under which the taken options are most likely, treating each choice as a softmax
over the options' totals (sum of value times weight). This is the same scoring as Arbiter's Decision, so weights
fitted here can be used as a decision's weights directly, up to a scale the caller sets for its own noise.

Terms can be grouped: a term with a group is pulled toward its group's weight (an L2 prior, PRIOR), and the group's
weight is fitted too, so a term seen in only a few choices stays near its group rather than swinging far. A term
without a group is fitted freely, more slowly.

It knows nothing of any game: terms are names, options are dictionaries. Plain Python, nothing to install.

    from fit_choices import Choice, fit
    weights, groups = fit(choices, group_of={"upgrade:health": "stats", ...})
"""
import math
from dataclasses import dataclass, field

PRIOR = 1.0        # How strongly a grouped term is pulled toward its group's weight.
STEPS = 4000       # Gradient steps.
RATE = 0.05        # Step size for grouped terms.
GROUP_RATE = 0.5   # A group's step, as a share of RATE.
FREE_RATE = 0.25   # An ungrouped term's step, as a share of RATE.


@dataclass
class Choice:
    """One recorded choice: each option's term values (missing terms count 0), and the index taken."""
    options: list = field(default_factory=list)  # list of dict: term name -> value
    taken: int = 0


def fit(choices, group_of=None, prior=PRIOR, steps=STEPS, rate=RATE):
    """
    Returns (weights, group_weights): a weight for every term seen in the choices, and for every group named in
    group_of. Terms not in group_of are fitted without a prior.
    """
    group_of = group_of or {}
    terms = sorted({t for c in choices for o in c.options for t in o})
    weights = {t: 0.0 for t in terms}
    groups = {g: 0.0 for g in sorted(set(group_of.values()))}
    for _ in range(steps):
        grad = {t: 0.0 for t in terms}
        group_grad = {g: 0.0 for g in groups}
        for c in choices:
            totals = [sum(weights[t] * v for t, v in o.items()) for o in c.options]
            top = max(totals)
            e = [math.exp(s - top) for s in totals]
            whole = sum(e)
            for i, o in enumerate(c.options):
                g = (1.0 if i == c.taken else 0.0) - e[i] / whole
                for t, v in o.items():
                    grad[t] += g * v
        for t in terms:
            group = group_of.get(t)
            if group is not None:
                grad[t] -= prior * (weights[t] - groups[group])
                group_grad[group] += prior * (weights[t] - groups[group])
        for t in terms:
            weights[t] += rate * grad[t] * (1.0 if t in group_of else FREE_RATE)
        for g in groups:
            groups[g] += rate * group_grad[g] * GROUP_RATE
    return weights, groups
