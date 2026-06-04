extends Node2D

const CARD_COUNT = 3
const CARD_SCENE_PATH = "res://card.tscn"

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

func add_card_to_hand(card):
	player_hand.insert(0, card)
	update_card_hand_position()

func update_card_hand_position():
	for i in range(player_hand.size()):
		var new_position = calculate_card_position(i)

func calculate_card_position(position):
	var total_width = (player_hand.size())

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
