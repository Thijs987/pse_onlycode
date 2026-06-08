extends Node2D

@onready var card_logic = $"../CardLogic"

const CARD_COUNT = 5
const CARD_SCENE_PATH = "res://card.tscn"
const CARD_WIDTH = 120
const HAND_Y = 500

var player_hand = []
var center_screen_x

@export var hand_curve = Curve
@export var rotation_curve = Curve

@export var max_rotation := 5
@export var separation := -10
@export var y_min := 0
@export var y_max := -15


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	center_screen_x = get_viewport().size.x / 2
	var card_scene = preload(CARD_SCENE_PATH)
	for i in range(CARD_COUNT):
		var new_card = card_scene.instantiate()
		$"../CardLogic".add_child(new_card)
		new_card.name = "Card"
		add_card_to_hand(new_card)

func _process(_delta: float) -> void:
	if card_logic and card_logic.dragging_card != null:
		sort_hand()

func new_card():
	var card_scene = preload(CARD_SCENE_PATH)
	var new_card = card_scene.instantiate()
	$"../CardLogic".add_child(new_card)
	new_card.name = "Card"
	add_card_to_hand(new_card)

func add_card_to_hand(card):
	if card not in player_hand:
		player_hand.insert(0, card)
		update_card_hand_position()
	else:
		var card_index = player_hand.find(card)
		if card_index != -1:
			card.z_index = player_hand.size() - card_index
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
	# Ook iets waardoor je alleen de kaart van positie verandert als ie niet te ver van je hand is
	var mouse_pos = get_global_mouse_position()
	if mouse_pos.y < 250: # Zoek juiste value hiervoor, miss ook minimum
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
