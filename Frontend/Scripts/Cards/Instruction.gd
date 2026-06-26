extends CanvasLayer

@onready var label: Label = $Panel/Label
@onready var panel: Panel = $Panel

func _ready() -> void:
	layer = 100
	visible = false

# Display the message after placing down a card
func display_message(new_text: String) -> void:
	if label == null:
		return

	label.text = new_text

	if new_text.is_empty():
		visible = false
	else:
		visible = true
