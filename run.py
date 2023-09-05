import argparse
import os
import json
import psutil
from dqn_trainer import run_dqn
from simple_trainer import run_simple, ACTION_SELECTION_MODE_RANDOM, ACTION_SELECTION_MODE_NULL

def find_start_port(num_envs):
    used_ports = set()
    for conn in psutil.net_connections():
        if conn.status != 'TIME_WAIT':
            used_ports.add(conn.laddr.port)
    start_port = 12000
    end_port = 60000
    num_ports = num_envs*2
    while True:
        if start_port + num_ports > end_port:
            raise Exception('failed to find sufficient open port range')
        range_ok = True
        for port in range(start_port, start_port + num_ports):
            if port in used_ports:
                start_port = port+1
                range_ok = False
        if range_ok:
            break
    print('Using ports [{}, {}]'.format(start_port, start_port + num_ports - 1))
    return start_port

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--predict', dest='is_predict', default=False, action='store_true')
    parser.add_argument('--deterministic', dest='is_deterministic', default=False, action='store_true')
    parser.add_argument('--workdir', default='workdir')
    parser.add_argument('--startport', default=None)
    parser.add_argument('config', metavar='CONFIG')
    args = parser.parse_args()

    if args.is_deterministic and not args.is_predict:
        raise Exception('--deterministic only valid with --predict')

    game_config_path = os.path.abspath(args.config)
    with open(game_config_path, 'r') as f:
        game_config = json.loads(f.read())
    if args.startport is None:
        start_port = find_start_port(game_config['num_envs'])
    else:
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
