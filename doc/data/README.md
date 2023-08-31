# Experiment Data

This directory contains CSV files with the raw experiment data for the three RQs in the paper.

Configurations starting `dqn_s84x4_count_` refer to the curiosity-driven reinforcement learning strategy, while those starting with `random_` refer to the random strategy.

Configurations ending with `_aa` are those using the action analysis action spaces, while those ending with `_manual` use the manually defined action spaces, and finally those ending `_b` refer to the generic Blind action space consisting of common keyboard and mouse actions.

The `null` configuration was run with no inputs sent to the game to establish a lower bound for exploration coverage.

For the RQ3 data, configurations ending in `_noma` refer to the "Slicing On, Mouse Off" configuration, `_nops` refers to "Slicing Off, Mouse On", and `_nops_noma` refers to "Slicing Off, Mouse Off".
