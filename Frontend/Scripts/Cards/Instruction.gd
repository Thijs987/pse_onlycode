extends CanvasLayer

@onready var label: Label = $Panel/Label

func _ready() -> void:
	layer = 100
	visible = false

func display_message(new_text: String) -> void:
	if label == null:
		return

	label.text = new_text
	if new_text.is_empty():
		visible = false
	else:
		visible = true
