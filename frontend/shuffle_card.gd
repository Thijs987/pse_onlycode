extends Sprite2D


# Important constants.
# Scale of the card
const xScale = 0.33
const yScale = 0.33

# Position the card has to move to
var goal = Vector2(500, 200)

var ani = false

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	if ani:
		if position.distance_to(goal) > 1:
			position = position.move_toward(goal, 300 * delta)
		else:
			position = goal
			ani = false
	
func _unhandled_input(event):
	if event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_LEFT:
			var click_pos = event.position
			var card_size = Vector2(500, 750)
			var scaled_size = card_size * Vector2(xScale, yScale)
			var card_pos = position
			var rect = Rect2(card_pos, scaled_size)
			if rect.has_point(click_pos):
				ani = true
