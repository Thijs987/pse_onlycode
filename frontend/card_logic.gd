extends Node2D

var screen_size
var dragging_card

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	screen_size = get_viewport_rect().size
	

var card_offsetx
var card_offsety

var play_area = Vector2(0, 200)
var play_pile = Vector2(200, 200)

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(_delta: float) -> void:
	if dragging_card != null:
		var mouse_pos = get_global_mouse_position()
		dragging_card.position = Vector2(clamp(mouse_pos.x + card_offsetx, 0, screen_size.x),
			clamp(mouse_pos.y + card_offsety, 0, screen_size.y))

# Listens to mouse input to call events when pressed
func _input(event):
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.is_pressed():
			var card = check_for_card()
			print(card)
			if card != null:
				start_dragging(card)
		else:
			stop_dragging()

		if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
			if event.is_released():
				var card = check_for_card()
				if card != null:
					play_card(card)
				
func play_card(card):
	if card.position.y < play_area.y:
		card.position = play_pile

func start_dragging(card):
	card.scale = Vector2(1.0, 1.0)
	dragging_card = card
	var card_pos = dragging_card.position
	var mouse_pos = get_global_mouse_position()
	card_offsetx = card_pos.x - mouse_pos.x
	card_offsety = card_pos.y - mouse_pos.y

func stop_dragging():
	if dragging_card:
		dragging_card.scale = Vector2(1.1, 1.1)
	dragging_card = null

func connect_card_signals(card):
	card.connect("hovered", hovered_over_card)
	card.connect("hovered_away", hovered_away_card)

var is_hovering

func hovered_over_card(card):
	if !is_hovering:
		is_hovering = true
		highlight_card(card, true)

func hovered_away_card(card):
	highlight_card(card, false)
	var check_card = check_for_card()
	if check_card:
		highlight_card(check_card, true)
	else:
		is_hovering = false

func highlight_card(card, hovered):
	if hovered:
		card.z_index = 2
		card.scale = Vector2(1.1, 1.1)
	else:
		card.z_index = 0
		card.scale = Vector2(1.0, 1.0)

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
