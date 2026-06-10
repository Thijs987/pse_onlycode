extends Node2D

signal left_mouse_pressed
signal left_mouse_release

const COLLISION_MASK_CARD = 1
const COLLISION_MASK_PILE = 2

var card_logic_reference
var pile_reference

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	card_logic_reference = $"../CardLogic"
	pile_reference = $"../Pile"

func _input(event):
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.is_pressed():
			var mask_id = check_at_cursor(true)
			emit_signal("left_mouse_pressed")
		else:
			var mask_id = check_at_cursor(false)
			emit_signal("left_mouse_release")
			
func check_at_cursor(is_pressed):
	var space_state = get_world_2d().direct_space_state
	var parameters = PhysicsPointQueryParameters2D.new()
	parameters.position = get_global_mouse_position()
	parameters.collide_with_areas = true
	var result = space_state.intersect_point(parameters)
	var result_size = result.size()
	if result.size() > 0:
		var result_collision_mask = result[0].collider.collision_mask
		if result_collision_mask == COLLISION_MASK_CARD:
			var card_found = result[0].collider.get_parent()
			if card_found and "movable" in card_found:
				if card_found.movable == true:
					card_logic_reference.start_dragging(card_found)
		elif result_collision_mask == COLLISION_MASK_PILE:
			if is_pressed:
				pile_reference.decrease_counter()

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
