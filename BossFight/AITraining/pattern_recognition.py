import pandas as pd
import json

# Load CSV
data = pd.read_csv(
    r"C:/Users/Pratik Kalloli/AppData/LocalLow/DefaultCompany/Action/combat_logs.csv"
)


print("\n===== PLAYER ANALYSIS =====\n")
avg_distance = data["Distance"].mean()

print("\n===== DISTANCE ANALYSIS =====")
print("Average Distance :", round(avg_distance, 2))

# Total actions
total_actions = len(data)

# Count actions
attack_count = (data["Action"] == "Attack").sum()
block_count = (data["Action"] == "Block").sum()
sprint_count = (data["Action"] == "Sprint").sum()

# Calculate percentages
attack_percent = (attack_count / total_actions) * 100
block_percent = (block_count / total_actions) * 100
sprint_percent = (sprint_count / total_actions) * 100

print(f"Total Actions : {total_actions}")
print(f"Attack Count  : {attack_count}")
print(f"Block Count   : {block_count}")
print(f"Sprint Count  : {sprint_count}")

print("\n===== ACTION PERCENTAGES =====\n")

print(f"Attack % : {attack_percent:.2f}")
print(f"Block %  : {block_percent:.2f}")
print(f"Sprint % : {sprint_percent:.2f}")

# Movement Pattern
left_moves = (data["MoveX"] < 0).sum()
right_moves = (data["MoveX"] > 0).sum()

print("\n===== MOVEMENT PATTERN =====\n")

print(f"Left Moves  : {left_moves}")
print(f"Right Moves : {right_moves}")

if left_moves > right_moves:
    movement_style = "Left Preferred"
elif right_moves > left_moves:
    movement_style = "Right Preferred"
else:
    movement_style = "Balanced Movement"

print("Movement Style :", movement_style)

# Player Classification
print("\n===== PLAYER TYPE =====\n")

# Player Classification

if attack_percent > 60:
    player_type = "Aggressive"

elif block_percent > 25:
    player_type = "Defensive"

elif avg_distance > 5:
    player_type = "HitAndRun"

elif avg_distance < 2:
    player_type = "CloseRangeFighter"

else:
    player_type = "Balanced"

print("Player Type :", player_type)

print("\n===== BOSS RESPONSE =====\n")

if player_type == "Aggressive":
    boss_strategy = "Counter Attacks"

elif player_type == "Defensive":
    boss_strategy = "Heavy Attacks"

elif player_type == "HitAndRun":
    boss_strategy = "Fast Chase"

elif player_type == "CloseRangeFighter":
    boss_strategy = "Strong Combo Attacks"

else:
    boss_strategy = "Balanced Combat"
    
print("Boss Strategy :", boss_strategy)
with open("player_profile.json", "w") as f:
    
    profile = {"PlayerType": player_type,
    "MovementStyle": movement_style,
    "BossStrategy": boss_strategy,
    "AttackPercentage": round(attack_percent, 2),
    "BlockPercentage": round(block_percent, 2),
    "SprintPercentage": round(sprint_percent, 2)
}

with open("player_profile.json", "w") as f:
    json.dump(profile, f, indent=4)

print("Player profile saved!")