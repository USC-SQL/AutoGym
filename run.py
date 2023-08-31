import argparse
import os
import json
from dqn_trainer import run_dqn
from simple_trainer import run_simple, ACTION_SELECTION_MODE_RANDOM, ACTION_SELECTION_MODE_NULL

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--predict', dest='is_predict', default=False, action='store_true')
    parser.add_argument('--deterministic', dest='is_deterministic', default=False, action='store_true')
    parser.add_argument('--workdir', default='workdir')
    parser.add_argument('--startport', default='12000')
    parser.add_argument('config', metavar='CONFIG')
    args = parser.parse_args()

    if args.is_deterministic and not args.is_predict:
        raise Exception('--deterministic only valid with --predict')

    game_config_path = os.path.abspath(args.config)
    with open(game_config_path, 'r') as f:
        game_config = json.loads(f.read())
    start_port = int(args.startport)
    workdir = os.path.abspath(args.workdir)
    is_predict = args.is_predict
    is_deterministic = args.is_deterministic

    trainer_name = game_config['trainer']
    if trainer_name == 'dqn':
        run_dqn(game_config, game_config_path, start_port, workdir, is_predict, is_deterministic)
    elif trainer_name == 'random':
        run_simple(game_config, game_config_path, start_port, workdir, is_predict, is_deterministic, ACTION_SELECTION_MODE_RANDOM)
    elif trainer_name == 'null':
        run_simple(game_config, game_config_path, start_port, workdir, is_predict, is_deterministic, ACTION_SELECTION_MODE_NULL)
    else:
        raise Exception("unrecognized trainer '{}'".format(trainer_name))
