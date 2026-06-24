extends Node2D

@onready var time_label: Label = $"../Background/TimeLabel"
@onready var tutorial_panel: Panel = $"TutorialPanel"
@onready var tutorial_label: Label = $TutorialPanel/TutorialLabel
@onready var turn_timer: Timer = $"../TurnTimer"
@onready var turn_label: Label = $"../Background/TurnLabel"
@onready var exit_button = $"ExitButton"
#References other scripts
@onready var hand_reference = $"../PlayerHand"
@onready var card_logic = $"../CardLogic"
@onready var pile_reference = $"../Pile"
@onready var discard_reference = $"../DiscardPile"

var player_list
var turns
var can_play
var can_hover
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
	exit_button.pressed.connect(_on_exit_button)
	if turn_label != null:
		turn_label.text = str(player_list[0])
	if tutorial_label != null:
		tutorial_panel.global_position.x = hand_reference.center_screen_x * 0.1
		tutorial_panel.global_position.y = hand_reference.center_screen_y
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

func _on_exit_button():
	SceneLoader.load_scene("uid://ctined7qq8dh2") #StartMenu.tscn uid
	
func do_play_card(card_id):
	card_logic.play_card(card_id)

func set_tutorial_text(text):
	tutorial_label.text = text
	tutorial_label.reset_size()
	tutorial_panel.z_index = 1000
	tutorial_panel.size = tutorial_label.size
	tutorial_panel.show()

func run_tutorial():
	set_tutorial_text("Welcome to the tutorial\nClick to continue...")
	wait_for_mouse_click = true
	await next_step
	wait_for_mouse_click = false
	set_tutorial_text("Draw a card by clicking on the draw pile")
	can_draw = true
	card_to_draw = "ddos"
	await next_step
	can_draw = false
	set_tutorial_text("Well done.\nDrawing a card will end your turn\nNow the other player will draw a card.")
	await get_tree().create_timer(1.0).timeout
	hand_reference.add_new_card("achterkant", 1)
	can_hover = true
	await get_tree().create_timer(1.0).timeout
	set_tutorial_text("Cards have special effects\nSee the effects by hovering over a card.")
	wait_for_hover = true
	await next_step
	wait_for_hover = false
	set_tutorial_text("For example, with this card\nthe other player has to take 2 turns,\nand you skip drawing a card\nNow play this card")
	can_play = true
	await next_step
	can_play = false
	await get_tree().create_timer(1.0).timeout
	hand_reference.add_new_card("achterkant", 1)
	set_tutorial_text("Your opponent now has 5 cards\nThis game has a card limit that starts at 5\nThe limit reduces everytime the draw pile is empty\nYour opponent now has to play a card or they lose\nClick to continue...")
	wait_for_mouse_click = true
	await next_step
	wait_for_mouse_click = false
	discard_reference.play_card(1, "cm")
	hand_reference.remove_card_from_hand(hand_reference.player_hands[1][0], 1)
	set_tutorial_text("Now it is your turn again\nYour turn is limited to 30 seconds\nIf the timer runs out before you end your turn,\na card will be drawn for you")
	turn_timer.start()
	can_draw = true
	card_to_draw = "nocom"
	await next_step
	can_draw = false
	turn_timer.stop()
	set_tutorial_text("You've drawn a blank card\nYou can only play this card in pairs of the same type")
	await get_tree().create_timer(1.0).timeout
	hand_reference.add_new_card("nocom", 0)
	set_tutorial_text("We've given you a second no comments card\nNow play both blank cards")
	can_play = true
	await next_step
	can_play = false
	hand_reference.add_new_card("achterkant", 1)
	set_tutorial_text("Congratulations, you won\nNow go play an entire match")
	await get_tree().create_timer(20.0).timeout
	SceneLoader.load_scene("uid://bdxchheqi80lr") #multilobby
	
