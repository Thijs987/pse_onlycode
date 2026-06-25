extends Node2D

@onready var card_logic = $"../CardLogic"

@export var hand_curve = Curve
@export var rotation_curve = Curve

@export var max_rotation := 5
@export var separation := -10
@export var y_min := 0
@export var y_max := -15

const CARD_SCENE_PATH = "uid://dlb3crw3qdkv2"
const CARD_WIDTH = 120
const CARD_HEIGHT = 300

var player_hands = [[], []]
var player_amount = 2
var center_screen_y
var center_screen_x


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	center_screen_x = get_viewport().size.x / 2
	center_screen_y = get_viewport().size.y / 2
	add_new_card("achterkant", 1)
	add_new_card("achterkant", 1)

func _process(_delta: float) -> void:
	if card_logic and card_logic.dragging_card != null:
		sort_hand()

func add_new_card(card_id, player_number):
	var card_scene = preload(CARD_SCENE_PATH)
	var new_card = card_scene.instantiate()

	card_logic.add_child(new_card)

	new_card.name = "Card"
	new_card.set_card(card_id)
	var pile_pos = $"../Pile/PileArea/Pile".global_position
	new_card.global_position = pile_pos
	if player_number == 0:
		new_card.is_others = false
	else:
		new_card.is_others = true
	player_hands[player_number].insert(0, new_card)
	update_card_hand_position(player_number)

	animate_draw_card(new_card)

func animate_draw_card(card):

	card.scale = Vector2(0.5, 0.5)

	var tween = get_tree().create_tween()
	tween.set_parallel(true)

	tween.tween_property(card, "global_position", card.hand_position, 0.4)
	tween.tween_property(card, "scale", Vector2.ONE, 0.4)

func add_card_to_hand(card, player_number):
	if card not in player_hands[player_number]:
		player_hands[player_number].insert(0, card)
		update_card_hand_position(player_number)
	else:
		if not card.has_meta("pending") or not card.get_meta("pending"):
			card.movable = true
		update_card_hand_position(player_number)
		move_to_position(card, card.hand_position)

func remove_card_from_hand(card, player_number):
	if card in player_hands[player_number]:
		card_logic.dragging_card = null
		player_hands[player_number].erase(card)
		card.queue_free()
		update_card_hand_position(player_number)

func update_card_hand_position(player_number):
	for i in range(player_hands[player_number].size()):
		var new_position = Vector2(calculate_card_x_position(i, player_number), calculate_card_y_position(i, player_number))
		var moving_card = player_hands[player_number][i]
		if player_number == 1:
			moving_card.rotation = PI
		if moving_card.movable == true:
			moving_card.hand_position = new_position
			var basis_z = player_hands[player_number].size() - i

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
	var moving_index = player_hands[0].find(moving_card)
	if moving_index == -1: # Card not found
		return

	if moving_index > 0: # Not left most card
		var left_card = player_hands[0][moving_index - 1]
		if mouse_pos.x < left_card.hand_position.x:
			player_hands[0][moving_index] = left_card
			player_hands[0][moving_index - 1] = moving_card
			update_card_hand_position(0)
			return
	if moving_index < player_hands[0].size() - 1: # Not right most card
		var right_card = player_hands[0][moving_index + 1]
		if mouse_pos.x > right_card.hand_position.x:
			player_hands[0][moving_index] = right_card
			player_hands[0][moving_index + 1] = moving_card
			update_card_hand_position(0)
			return

func calculate_card_x_position(position, player_number):
	var total_width = (player_hands[player_number].size() - 1) * CARD_WIDTH
	var x_offset = center_screen_x + position * CARD_WIDTH - total_width / 2
	return x_offset

func calculate_card_y_position(position, player_number):
	if player_number == 0:
		return center_screen_y * 2 - CARD_HEIGHT * 0.1
	else:
		return 0 + CARD_HEIGHT * 0.1

func move_to_position(card, position):
	var tween = get_tree().create_tween()
	tween.tween_property(card, "position", position, 0.2)
