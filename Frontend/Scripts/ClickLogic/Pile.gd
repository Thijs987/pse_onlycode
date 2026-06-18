extends Node2D

@onready var counter_label: Label = $PileArea/CounterLabel
@onready var hand_reference = $"../PlayerHand"
@onready var CardLogic = $"../CardLogic"
# Deze aanpassen voor het goeie aantal kaarten
@export var card_count: int = 40

var turns = null

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	update_card_text()
	hand_reference.next_turn.connect(_newturn)
	controller.message_updated.connect(_on_message)

func _on_message(msg):
	if msg.get("action") == "MATCH_STARTED":
		var new_size = msg.get("data", {}).get("deckSize")
		if new_size != null:
			card_count = int(new_size)
			update_card_text()

	elif msg.get("action") == "DECK_SIZE":
		var new_size = msg.get("data", {}).get("message")
		if new_size != null:
			card_count = int(new_size)
			update_card_text()

	elif msg.get("action") == "CARD_DRAWN":
		if card_count > 0:
			card_count -= 1
			update_card_text()
		
		if msg.get("playerId") == controller.PId:
			var drawn_card = msg.get("data", {}).get("cardId")
			if drawn_card != null and drawn_card != "":
				hand_reference.add_new_card(drawn_card, 0)

func _newturn(player):
	if player != null:
		turns = player

# Haalt 1 kaart van de counter
func decrease_counter():
	if controller.interaction_disabled:
		return
		
	if CardLogic.first_combo_card != null or CardLogic.trojan_selecting_gift == true:
		print("Cant draw card when playing cards")
		return
	elif CardLogic.imp_hardware_active == true or CardLogic.os_active == true:
		print("Cant draw card when playing cards")
		return
	
	if turns == controller.PId:
		if card_count > 0:
			controller.Draw_Card(controller.PId)
		else:
			print("Pile is empty")

# Past card count getal aan
func update_card_text():
	if counter_label != null:
		counter_label.text = str(card_count)
