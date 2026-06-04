extends Node2D

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
			move_to_position(moving_card, new_position)
		

func calculate_card_position(position):
	var total_width = (player_hand.size() - 1) * CARD_WIDTH
	var x_offset = center_screen_x + position * CARD_WIDTH - total_width / 2
	return x_offset

func move_to_position(card, position):
	var tween = get_tree().create_tween()
	tween.tween_property(card, "position", position, 0.2)
