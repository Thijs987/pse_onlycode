extends Node2D

@onready var card_logic = $"../CardLogic"
@onready var turn_label: Label = $"../Background/TurnLabel"

@export var hand_curve = Curve
@export var rotation_curve = Curve

@export var max_rotation := 5
@export var separation := -10
@export var y_min := 0
@export var y_max := -15

const CARD_COUNT = 0
const CARD_SCENE_PATH = "uid://dlb3crw3qdkv2"
const CARD_WIDTH = 120
const HAND_Y = 500

var player_hand = []
var center_screen_x

signal next_turn(player)


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if controller.Last_Message["action"] == "MATCH_STARTED":
		var player = controller.Last_Message["data"]["nextPlayer"]
		if player != null:
			next_turn.emit(player)
			turn_label.text = str(player)
	
	controller.message_updated.connect(_on_message)
	center_screen_x = get_viewport().size.x / 2
	var card_scene = preload(CARD_SCENE_PATH)
	for i in range(CARD_COUNT):
		var new_card = card_scene.instantiate()
		card_logic.add_child(new_card)
		new_card.name = "Card"
		new_card.set_card("goto")
		add_card_to_hand(new_card)

func _process(_delta: float) -> void:
	if card_logic and card_logic.dragging_card != null:
		sort_hand()

func _on_message(msg):
	if msg != null:
		if msg["action"] == "NEXT_TURN":
			var player = msg["data"]["nextPlayer"]
			if player != null:
				next_turn.emit(player)
				turn_label.text = str(player)
		if msg["action"] == "CARD_PLAYED":
			var player = msg["data"]["nextPlayer"]
			if player != null and player != "":
				next_turn.emit(player)
				turn_label.text = str(player)

func add_new_card(card_id):
	var card_scene = preload(CARD_SCENE_PATH)
	var new_card = card_scene.instantiate()
	card_logic.add_child(new_card)
	new_card.name = "Card"
	new_card.set_card(card_id)
	add_card_to_hand(new_card)

func add_card_to_hand(card):
	if card not in player_hand:
		player_hand.insert(0, card)
		update_card_hand_position()
	else:
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
