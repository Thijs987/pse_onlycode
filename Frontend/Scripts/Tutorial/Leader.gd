extends Node2D

@onready var time_label: Label = $"../Background/TimeLabel"
@onready var tutorial_label: Label = $TutorialLabel
@onready var turn_timer: Timer = $"../TurnTimer"
@onready var turn_label: Label = $"../Background/TurnLabel"
@onready var hand_reference = $"../PlayerHand"
@onready var card_logic = $"../CardLogic"
@onready var pile_reference = $"../Pile"

var player_list
var turns
var can_play
var can_draw
var wait_for_mouse_click
var wait_for_hover
var card_to_draw

signal next_step(action)

func _ready() -> void:
	player_list = [controller.PId, "bot"]
	turns = player_list[0]
	turn_timer.timeout.connect(_on_timeout)
	card_logic.card_played.connect(_on_card_played)
	card_logic.card_hovered.connect(_on_card_hovered)
	pile_reference.card_drawn.connect(_on_card_drawn)
	if turn_label != null:
		turn_label.text = str(player_list[0])
	if tutorial_label != null:
		tutorial_label.global_position.x = hand_reference.center_screen_x * 0.25
		tutorial_label.global_position.y = hand_reference.center_screen_y
		tutorial_label.z_index = 4000
	if time_label != null:
		time_label.visible = false
	can_play = false
	can_draw = false
	wait_for_hover = false
	wait_for_mouse_click = false
	run_tutorial()

func _on_timeout():
	pass#turn_timer.start()

func _on_card_played(player_id, card_id):
	if player_id == player_list[0]:
		turns = player_list[1]
		next_step.emit()

func _on_card_hovered():
	if wait_for_hover:
		next_step.emit()

func _on_card_drawn():
	hand_reference.add_new_card(card_to_draw, 0)
	next_step.emit()

func _input(event):
	if event is InputEventMouseButton and wait_for_mouse_click:
		if event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
			next_step.emit()

func do_play_card(card):
	card_logic.play_card(card)

func run_tutorial():
	tutorial_label.text = "Welcome to the tutorial"
	wait_for_mouse_click = true
	await next_step
	wait_for_mouse_click = false
	tutorial_label.text = "Draw a card by clicking on the draw pile"
	can_draw = true
	card_to_draw = "cm"
	await next_step
	can_draw = false
	tutorial_label.text = "Well done.\nDrawing a card will end your turn\nNow the other player will draw a card."
	await get_tree().create_timer(1.0).timeout
	hand_reference.add_new_card("achterkant", 1)
	wait_for_mouse_click = true
	await next_step
	wait_for_mouse_click = false
	tutorial_label.text = "Cards have special effects\nSee the effects by hovering over a card."
	can_play = true
	wait_for_hover = true
	await next_step
	wait_for_hover = false
	tutorial_label.text = "For example, with this card\nyou don't have to draw a card to end your turn\nNow play this card"
	await next_step
	can_play = false
	tutorial_label.text = "If you have to many cards in your hand, you will lose\nThe limit starts at 5,\nand reduces every time the draw pile is empty"
	
