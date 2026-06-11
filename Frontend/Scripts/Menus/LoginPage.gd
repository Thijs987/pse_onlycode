extends Control

@onready var text_label: Label = $VBoxContainer/HBoxContainer1/Label
@onready var name_input: LineEdit = $VBoxContainer/HBoxContainer2/LineEdit
@onready var name_button: Button = $VBoxContainer/HBoxContainer3/Button

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	name_button.pressed.connect(_on_name_button_pressed)

func _on_name_button_pressed():
	controller.PId = name_input.text
	text_label.text = controller.PId
	name_input.clear()
