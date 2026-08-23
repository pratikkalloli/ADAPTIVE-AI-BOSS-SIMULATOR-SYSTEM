import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.preprocessing import LabelEncoder
import joblib

# Load data
data = pd.read_csv("combat_logs.csv")

# Convert action text to numbers
action_encoder = LabelEncoder()
data["Action"] = action_encoder.fit_transform(data["Action"])

# Create target column
data["NextAction"] = data["Action"].shift(-1)

# Remove last row
data = data.dropna()

# Features
X = data[[
    "Action",
    "MoveX",
    "MoveY"
]]

# Target
y = data["NextAction"]

# Train model
model = RandomForestClassifier(
    n_estimators=100,
    random_state=42
)

model.fit(X, y)

joblib.dump(model, "boss_model.pkl")

print("Training Complete")