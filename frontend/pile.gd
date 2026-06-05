extends Node2D

# Deze aanpassen voor het goeie aantal kaarten
@export var card_count: int = 40

# Hier kan dan de echte pile voor de database geimporteerd worden
# (Import line)

# Maakt CounterLabel node
@onready var counter_label: Label = $Pile_area/CounterLabel
@onready var controller = $"../Controller"
@onready var hand_reference = $"../PlayerHand"

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	update_card_text()

# Haalt 1 kaart van de counter
func decrease_counter():
	if card_count > 0:
		card_count -= 1
		# Past card count getal aan
		update_card_text()
		controller.Draw_Card("Player_1")
		hand_reference.new_card()
	else:
		print("Pile is empty")

# Past card count getal aan
func update_card_text():
	counter_label.text = str(card_count)

# Hier moet een functie die de volgende kaart uit de string/array pakt
# func ...
