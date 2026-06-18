extends CanvasLayer

@onready var grab_button: TextureButton = $VBoxContainer/GrabCard
@onready var top_button: TextureButton = $VBoxContainer/PlaceOnTop

signal choice_selected(choice_string)

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if grab_button:
		grab_button.pressed.connect(_on_grab_pressed)
	if top_button:
		top_button.pressed.connect(_on_top_pressed)


func _on_grab_pressed():
	print("Putting card in your hand")
	choice_selected.emit("take")
	queue_free()
	
func _on_top_pressed():
	print("Putting card on top of your pile and ending turn")
	choice_selected.emit("top")
	queue_free()
