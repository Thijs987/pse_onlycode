extends Node2D

@onready var discard_pile = $"../DiscardPile"
@onready var hand_reference = $"../PlayerHand"
@onready var leader = $"../Leader"

const CARD_COLLISION_MASK = 1
const ATTACK_SCENE = preload("res://Scenes/Attack.tscn")

var screen_size
var dragging_card
var is_hovering
var card_offsetx
var card_offsety
var tooltip_scene = preload("uid://b0ems5mni4412")
var card_tooltip
var first_combo_card = null

signal card_played(player_id, card_id)
signal card_hovered

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
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

# Logic for playing card
func play_card(card):
	if leader.can_play == false:
		return
	if discard_pile == null:
		return
	var discard_area = discard_pile.get_node_or_null("DiscardPileArea")
	if discard_area == null:
		return

	if discard_area.overlaps_area(card.get_node("Area2D")):
		var current_id = card.own_card_id
		if current_id != "nocom":
			card.set_meta("pending", true)
			card.modulate.a = 0.5

			highlight_card(card, false)
			card_played.emit(controller.PId, current_id)
			hand_reference.remove_card_from_hand(card, 0)
		else:
			if first_combo_card == null: # First blanco card played
				first_combo_card = card

				# Place the card visually
				first_combo_card.position.y -= 30
				first_combo_card.position.x -= 200
				first_combo_card.movable = false

			else: # Second blanco card
				# Checks if type matches or at least 1 card is a goto
				if current_id == first_combo_card.own_card_id or current_id == "goto" or first_combo_card.own_card_id == "goto":
					var played_cards = [first_combo_card.own_card_id, current_id]
					await sql_attack()
					card_played.emit(controller.PId, played_cards)

					# Play both cards
					hand_reference.remove_card_from_hand(first_combo_card, 0)
					hand_reference.remove_card_from_hand(card, 0)

					first_combo_card.queue_free()
					card.queue_free()

					first_combo_card = null # Reset combo flag

# Logic for sql attack
func sql_attack() -> String:
	var attack_screen = ATTACK_SCENE.instantiate()
	get_tree().root.add_child(attack_screen)
	
	if attack_screen is Control:
		attack_screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	
	var filtered_enemies = ["Opponent"]
	
	attack_screen.setup_targets(filtered_enemies)
	get_tree().paused = true # Pauses the game except for the selection menu
	var gekozen_id = await attack_screen.target_selected
	get_tree().paused = false
	return gekozen_id

# Called when the bot playes a card instead of the player
func bot_play_card(card, card_id):
	if leader.turns == leader.player_list[1]:
		card_played.emit(leader.player_list[1], card_id)
		hand_reference.remove_card_from_hand(card, 1)

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
		leader.do_play_card(dragging_card)
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
	if hovered and leader.can_hover:
		if "is_others" in card:
			if card.is_others == false:
				card_hovered.emit()
				card.scale = Vector2(1.1, 1.1)
				#if the mouse is below show_tooltip_y, show the tooltip
				var show_tooltip_y = hand_reference.center_screen_y * 2
				show_tooltip_y -= hand_reference.CARD_HEIGHT * 0.2
				if not dragging_card and card.global_position.y > show_tooltip_y:
					card_tooltip.show_tooltip(card.own_card_id)
					card_tooltip.global_position.x = card.global_position.x
					card_tooltip.global_position.y = show_tooltip_y
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
