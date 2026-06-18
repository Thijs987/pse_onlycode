extends Node2D

@onready var discard_pile = $"../DiscardPile"
@onready var hand_reference = $"../PlayerHand"

const CARD_COLLISION_MASK = 1

var screen_size
var dragging_card
var is_hovering
var card_offsetx
var card_offsety
var turns
var tooltip_scene = preload("uid://b0ems5mni4412")
var card_tooltip


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	hand_reference.next_turn.connect(_newturn)
	screen_size = get_viewport_rect().size
	$"../InputManager".connect("left_mouse_release", on_left_mouse_release)
	card_tooltip = tooltip_scene.instantiate()
	add_child(card_tooltip)


# Runs every frame, card is set to current mouse position with offset
func _process(_delta: float) -> void:
	if dragging_card != null:
		var mouse_pos = get_global_mouse_position()
		dragging_card.position = Vector2(clamp(mouse_pos.x + card_offsetx, 0, screen_size.x),
			clamp(mouse_pos.y + card_offsety, 0, screen_size.y))

func _newturn(player):
	if player != null:
		turns = player

### HIER DE LOGICA VOOR HET SPELEN VAN EEN KAART
func play_card(card):
	if controller.interaction_disabled:
		return
	if discard_pile == null:
		print("Error: DiscardPile node not found!")
		return
	var discard_area = discard_pile.get_node_or_null("DiscardPileArea")
	if discard_area == null:
		print("Error: DiscardPileArea node not found!")
		return

#	if discard_area.overlaps_area(card.get_node("Area2D")) and controller.PId == turns:

	if discard_area.overlaps_area(card.get_node("Area2D")) and turns == controller.PId:
		card.set_meta("pending", true)
		card.modulate.a = 0.5

		highlight_card(card, false)

		hand_reference.add_card_to_hand(card,0)

		controller.Play_Card(controller.PId, card.own_card_id)

# Starts dragging of current card under mouse.
# input: Card object found using check_at_cursor function
func start_dragging(card):
	if controller.interaction_disabled:
		return
	card.scale = Vector2(1.0, 1.0)
	dragging_card = card
	card.z_index = 99 # Above all other cards
	var card_pos = dragging_card.position
	var mouse_pos = get_global_mouse_position()
	card_offsetx = card_pos.x - mouse_pos.x
	card_offsety = card_pos.y - mouse_pos.y
	card_tooltip.hide_tooltip()

# Calls logic for case of stopping dragging when left mouse button is released
func stop_dragging():
	if dragging_card and dragging_card.movable == true:
		play_card(dragging_card)
	var released_card = dragging_card # Temp variable for add_card_to_hand
	if released_card and (released_card.movable == true or released_card.has_meta("pending")):
		released_card.scale = Vector2(1.1, 1.1)
		dragging_card = null

		hand_reference.add_card_to_hand(released_card, 0)
		if released_card.global_position == released_card.hand_position:
			highlight_card(released_card, true)

	dragging_card = null

# Connects the signals for various player actions
func connect_card_signals(card):
	card.connect("hovered", hovered_over_card)
	card.connect("hovered_away", hovered_away_card)

# Calls functions for case of releasing left mouse button
func on_left_mouse_release():
	stop_dragging()
	var card = check_for_card()

# sets is_hovering to true and highlights current card
func hovered_over_card(card):
	if !is_hovering and card.movable == true:
		is_hovering = true
		highlight_card(card, true)

# Checks wether the mouse is currently hovering over a card
# if not, sets is_hovering to false
func hovered_away_card(card):
	highlight_card(card, false)
	var check_card = check_for_card()
	if check_card and card.movable == true:
		highlight_card(check_card, true)
	else:
		is_hovering = false

# Will highlight the card if mouse is hovering over it
func highlight_card(card, hovered):
	if hovered:
		if "is_others" in card:
			if card.is_others == false:
				card.scale = Vector2(1.1, 1.1)
		if not dragging_card:
			card_tooltip.show_tooltip(card.own_card_id)
			card_tooltip.global_position = card.global_position
	else:
		card.scale = Vector2(1.0, 1.0)
		card_tooltip.hide_tooltip()

# Checks wether under the current mouse position is a card and returns
# the collider of the card
func check_for_card():
	var space_state = get_world_2d().direct_space_state
	var parameters = PhysicsPointQueryParameters2D.new()
	parameters.position = get_global_mouse_position()
	parameters.collide_with_areas = true
	parameters.collision_mask = 1
	var result = space_state.intersect_point(parameters)
	var result_size = result.size()
	if (result_size > 0):
		return highest_z(result)
	return null

# returns the card with the highest z value from a list of cards checked
# by check_for_card()
func highest_z(cards):
	var highest_card = cards[0].collider.get_parent()
	var highest_card_z = highest_card.z_index
	for i in range(0, cards.size()):
		var new_card = cards[i].collider.get_parent()
		var new_card_z = new_card.z_index
		if new_card_z > highest_card_z:
			highest_card = new_card
			highest_card_z = new_card_z
	return highest_card
