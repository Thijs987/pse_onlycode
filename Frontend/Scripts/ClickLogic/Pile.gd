extends Node2D

@onready var counter_label: Label = $PileArea/CounterLabel
@onready var hand_reference = $"../PlayerHand"
@onready var CardLogic = $"../CardLogic"
# Deze aanpassen voor het goeie aantal kaarten
@export var card_count: int = 40
var visual_cards = []
const CARD_SCENE = preload("uid://dlb3crw3qdkv2")
var turns = null
var updating_pile := false

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	# MATCH_STARTED is received in the lobby scene before this scene loads, so the
	# signal has already fired by the time we connect below. Read the initial deck
	# size from the stored last message instead of waiting for a (missed) signal.
	if controller.Last_Message.get("action") == "MATCH_STARTED":
		var new_size = controller.Last_Message.get("data", {}).get("deckSize")
		if new_size != null:
			card_count = int(new_size)
	update_card_text()
	create_visual_pile()

	hand_reference.next_turn.connect(_newturn)
	controller.message_updated.connect(_on_message)

func create_visual_pile():
	# Remove old cards
	for card in visual_cards:
		card.queue_free()

	visual_cards.clear()

	var amount = min(3, card_count)

	for i in range(amount):
		var card = CARD_SCENE.instantiate()

		add_child(card)

		card.set_card("achterkant")
		card.movable = false
		card.is_others = true

		card.get_node("Area2D").monitoring = false
		card.get_node("Area2D").monitorable = false
		card.get_node("Area2D").collision_layer = 0
		card.get_node("Area2D").collision_mask = 0

		setup_draw_pile_card(card, i)

		visual_cards.append(card)


func setup_draw_pile_card(card, index):
	var card_size = card.get_node("Sprite2D").texture.get_size()

	var target_scale = Vector2(
		300.0 / card_size.x,
		450.0 / card_size.y
	)

	card.scale = target_scale

	var pile_pos = $PileArea.global_position

	card.global_position = pile_pos + Vector2(
		index * 4,
		index * 4
	)

	card.rotation_degrees = randf_range(-4.0, 4.0)

	card.z_index = index
	
func update_visual_pile():

	if updating_pile:
		return

	updating_pile = true
	var wanted_cards = min(3, card_count)

	# Remove cards if needed
	while visual_cards.size() > wanted_cards:
		var card = visual_cards.pop_back()

		var tween = get_tree().create_tween()

		tween.parallel().tween_property(
			card,
			"scale",
			Vector2.ZERO,
			0.15
		)

		await tween.finished

		card.queue_free()
	updating_pile = false

	while visual_cards.size() < wanted_cards:
		var card = CARD_SCENE.instantiate()

		add_child(card)

		card.set_card("achterkant")
		card.movable = false
		card.is_others = true

		card.get_node("Area2D").monitoring = false
		card.get_node("Area2D").monitorable = false
		card.get_node("Area2D").collision_layer = 0
		card.get_node("Area2D").collision_mask = 0

		setup_draw_pile_card(card, visual_cards.size())

		visual_cards.append(card)

	# Reposition stack nicely
	for i in range(visual_cards.size()):
		setup_draw_pile_card(visual_cards[i], i)



func _on_message(msg):
	if msg.get("action") == "MATCH_STARTED":
		var new_size = msg.get("data", {}).get("deckSize")
		if new_size != null:
			card_count = int(new_size)
			update_card_text()
			update_visual_pile()

	elif msg.get("action") == "DECK_SIZE":
		var new_size = msg.get("data", {}).get("message")
		if new_size != null:
			card_count = int(new_size)
			update_card_text()
			update_visual_pile()
	elif msg.get("action") == "CARD_DRAWN":
		if card_count > 0:
			card_count -= 1
			update_card_text()
			update_visual_pile()
		
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
