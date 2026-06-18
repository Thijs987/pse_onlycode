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
	print("Putting card in your hand")
	choice_selected.emit("take")
	queue_free()
	
func _on_top_pressed():
	print("Putting card on top of your pile and ending turn")
	choice_selected.emit("top")
	queue_free()
	
	
func toon_kaart(card_id: String):
	# 1. Maak een instantie van de kaart aan
	var nieuwe_kaart = CARD_SCENE.instantiate()
	
	# 2. Voeg de kaart toe als kind van je SpawnPoint
	$CardSpawnPoint.add_child(nieuwe_kaart)
	
	# 3. Zet de kaart lokaal op (0, 0) zodat hij EXACT op de stip van je SpawnPoint staat
	nieuwe_kaart.position = Vector2.ZERO
	
	# 4. Verander de kaart naar de juiste ID (bijv. "os") via jouw bestaande functie
	nieuwe_kaart.set_card(card_id)
	
	# 5. Zorg dat de speler deze kaart in het menu niet per ongeluk kan wegslepen
	nieuwe_kaart.movable = false
	
	# 6. Mocht de kaart achter je menu-achtergrond verdwijnen, zet de z_index dan hoger:
	nieuwe_kaart.z_index = 101
