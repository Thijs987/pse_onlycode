extends Node2D

@onready var counter_label: Label = $PileArea/CounterLabel
@onready var hand_reference = $"../PlayerHand"

# Deze aanpassen voor het goeie aantal kaarten
@export var card_count: int = 40

var drawn_card = null


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	update_card_text()
	controller.message_updated.connect(_on_message)

func _on_message(msg):
	if controller.Last_Message["action"] == "CARD_DRAWN":
		drawn_card = controller.Last_Message["data"]["cardId"]

# Haalt 1 kaart van de counter
func decrease_counter():
	if card_count > 0:
		card_count -= 1
		# Past card count getal aan
		update_card_text()
		controller.Draw_Card(controller.PId)
		await get_tree().create_timer(0.25).timeout #DIT KAN EEN BUG WORDEN
		#DAN MOET JE DE TIMER LANGER MAKEN, OF EEN ANDERE MANIER OM TE WACHTEN OM _ON_MESSAGE
		hand_reference.add_new_card(drawn_card)
	else:
		print("Pile is empty")

# Past card count getal aan
func update_card_text():
	counter_label.text = str(card_count)

# Hier moet een functie die de volgende kaart uit de string/array pakt
# func ...
