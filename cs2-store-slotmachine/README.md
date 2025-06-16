# [Store module] SlotMachine
SlotMachine module for Store: Bet credits, spin the slots, and win based on matching symbols. Symbols, chances and multipliers are customizable.

# Config
Config will be auto generated. Default:
```json
{
    "{red}[Store] ",
    "min_bet": 10,
    "max_bet": 1000,
    "slot_machine_commands": [
        "slotmachine"
    ],
    "slotmachine_command_cooldown": 10,
    "reward_multipliers": {
        "★": {
            "multiplier": 10,
            "chance": 2
        },
        "♞": {
            "multiplier": 8,
            "chance": 3
        },
        "⚓": {
            "multiplier": 6,
            "chance": 3
        },
        "☕": {
            "multiplier": 5,
            "chance": 4
        },
        "⚽": {
            "multiplier": 4,
            "chance": 4
        },
        "☀": {
            "multiplier": 3,
            "chance": 5
        },
        "☁": {
            "multiplier": 2,
            "chance": 5
        },
        "✿": {
            "multiplier": 15,
            "chance": 1
        },
        "☾": {
            "multiplier": 20,
            "chance": 0.5
        }
    },
    "slot_timers": {
        "first_stop": 1.0,
        "second_stop": 2.0,
        "third_stop": 3.0
    },
    "partial_win_percentage": 50,
    "sequential_symbols_only": false
}
```