extends CanvasLayer

@onready var grab_button: TextureButton = $VBoxContainer/GrabCard
@onready var top_button: TextureButton = $VBoxContainer/PlaceOnTop
const CARD_SCENE = preload("res://Scenes/Card.tscn")
signal choice_selected(choice_string)

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if grab_button:
		grab_button.pressed.connect(_on_grab_pressed)
	if top_button:
		top_button.pressed.connect(_on_top_pressed)


func _on_grab_pressed():
	choice_selected.emit("take")
	queue_free()
	
func _on_top_pressed():
	choice_selected.emit("top")
	queue_free()
	
	
func toon_kaart(card_id: String):
	for child in $CardSpawnPoint.get_children():
		child.queue_free()

	var nieuwe_kaart = CARD_SCENE.instantiate()
	$CardSpawnPoint.add_child(nieuwe_kaart)
	nieuwe_kaart.position = Vector2.ZERO
	
	nieuwe_kaart.set_card(card_id)
	nieuwe_kaart.movable = false
	nieuwe_kaart.z_index = 101 # Just in case
