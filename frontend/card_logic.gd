extends Node2D

var screen_size
var dragging_card
var card_offsetx
var card_offsety

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	screen_size = get_viewport_rect().size
	


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	if dragging_card != null:
		var mouse_pos = get_global_mouse_position()
		dragging_card.position = Vector2(clamp(mouse_pos.x + card_offsetx, 0, screen_size.x),
			clamp(mouse_pos.y + card_offsety, 0, screen_size.y))

# Listens to mouse input to call events when pressed
func _input(event):
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.is_pressed():
			var card = check_for_card()
			if card != null:
				dragging_card = card
				var card_pos = dragging_card.position
				var mouse_pos = get_global_mouse_position()
				card_offsetx = card_pos.x - mouse_pos.x
				card_offsety = card_pos.y - mouse_pos.y
		else:
			dragging_card = null

# Checks wether under the current mouse position is a card and returns
# the collider of the card
func check_for_card():
	var space_state = get_world_2d().direct_space_state
	var parameters = PhysicsPointQueryParameters2D.new()
	parameters.position = get_global_mouse_position()
	parameters.collide_with_areas = true
	parameters.collision_mask = 1
	var result = space_state.intersect_point(parameters)
	if (result.size() > 0):
		return result[0].collider.get_parent()
	return null
	
