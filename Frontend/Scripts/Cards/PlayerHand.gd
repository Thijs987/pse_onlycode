extends Node2D

@onready var card_logic = $"../CardLogic"
@onready var turn_label: Label = $"../Background/TurnLabel"

@export var hand_curve = Curve
@export var rotation_curve = Curve

@export var max_rotation := 5
@export var separation := -10
@export var y_min := 0
@export var y_max := -15

const CARD_SCENE_PATH = "uid://dlb3crw3qdkv2"
const CARD_WIDTH = 120
const HAND_Y = 500

var player_hand = []
var center_screen_x

signal next_turn(player)


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if controller.Last_Message.has("action") and controller.Last_Message["action"] == "MATCH_STARTED":
		var player = controller.Last_Message.get("data", {}).get("nextPlayer")
		if player != null:
			next_turn.emit(player)
			if turn_label != null:
				turn_label.text = str(player)

	controller.message_updated.connect(_on_message)
	center_screen_x = get_viewport().size.x / 2
	for card_id in controller.Player_Hand:
		add_new_card(card_id)

func _process(_delta: float) -> void:
	if card_logic and card_logic.dragging_card != null:
		sort_hand()

func _on_message(msg):
	if msg != null:
		if msg["action"] == "NEXT_TURN":
			var player = msg.get("data", {}).get("nextPlayer")
			if player != null:
				next_turn.emit(player)
				if turn_label != null:
					turn_label.text = str(player)

		if msg["action"] == "CARD_PLAYED":
			# Set turn to the next player
			var player = msg.get("data", {}).get("nextPlayer")
			if player != null and player != "":
				next_turn.emit(player)
				if turn_label != null:
					turn_label.text = str(player)
			
			var target = msg.get("data", {}).get("target")
			if target != null and target != "":
				var cards = msg.get("data", {}).get("")
				if target == controller.PId && cards != null:
					for card in cards:
						add_new_card(card)
				
			
			if msg.get("playerId") == controller.PId:
				var played_id = msg.get("data", {}).get("cardId")
				for i in range(player_hand.size()):
					if player_hand[i].own_card_id == played_id and player_hand[i].has_meta("pending"):
						var card = player_hand[i]
						player_hand.remove_at(i)
						card.queue_free()
						update_card_hand_position()
						break

		if msg["action"] == "ERROR":
			if msg.get("playerId") == controller.PId:
				for card in player_hand:
					if card.has_meta("pending") and card.get_meta("pending") == true:
						card.set_meta("pending", false)
						card.modulate.a = 1.0
						card.movable = true

func add_new_card(card_id):
	var card_scene = preload(CARD_SCENE_PATH)
	var new_card = card_scene.instantiate()

	card_logic.add_child(new_card)

	new_card.name = "Card"
	new_card.set_card(card_id)
	var pile_pos = $"../Pile/PileArea/Pile".global_position
	new_card.global_position = pile_pos
	player_hand.insert(0, new_card)
	update_card_hand_position()

	animate_draw_card(new_card)

func animate_draw_card(card):

	card.scale = Vector2(0.5, 0.5)

	var tween = get_tree().create_tween()
	tween.set_parallel(true)

	tween.tween_property(card, "global_position", card.hand_position, 0.4)
	tween.tween_property(card, "scale", Vector2.ONE, 0.4)

func add_card_to_hand(card):
	if card not in player_hand:
		player_hand.insert(0, card)
		update_card_hand_position()
		if card.own_card_id == "imp": # If you grab an improved hardware, you must play it
			print("Grabbed improved hardware, must play")
			card_logic.play_card(card)
	else:
		if not card.has_meta("pending") or not card.get_meta("pending"):
			card.movable = true
		update_card_hand_position()
		move_to_position(card, card.hand_position)

func remove_card_from_hand(card):
	if card in player_hand:
		player_hand.erase(card)
		update_card_hand_position()

func update_card_hand_position():
	for i in range(player_hand.size()):
		var new_position = Vector2(calculate_card_position(i), HAND_Y)
		var moving_card = player_hand[i]
		if moving_card.movable == true:
			moving_card.hand_position = new_position
			var basis_z = player_hand.size() - i

			if moving_card == card_logic.dragging_card:
				moving_card.z_index = basis_z + 100
			else:
				moving_card.z_index = basis_z
				move_to_position(moving_card, new_position)

func sort_hand():
	var mouse_pos = get_global_mouse_position()
	if mouse_pos.y < 250: # Card too high
		return

	var moving_card = card_logic.dragging_card
	var moving_index = player_hand.find(moving_card)
	if moving_index == -1: # Card not found
		return

	if moving_index > 0: # Not left most card
		var left_card = player_hand[moving_index - 1]
		if mouse_pos.x < left_card.hand_position.x:
			player_hand[moving_index] = left_card
			player_hand[moving_index - 1] = moving_card
			update_card_hand_position()
			return
	if moving_index < player_hand.size() - 1: # Not right most card
		var right_card = player_hand[moving_index + 1]
		if mouse_pos.x > right_card.hand_position.x:
			player_hand[moving_index] = right_card
			player_hand[moving_index + 1] = moving_card
			update_card_hand_position()
			return

func calculate_card_position(position):
	var total_width = (player_hand.size() - 1) * CARD_WIDTH
	var x_offset = center_screen_x + position * CARD_WIDTH - total_width / 2
	return x_offset

func move_to_position(card, position):
	var tween = get_tree().create_tween()
	tween.tween_property(card, "position", position, 0.2)
