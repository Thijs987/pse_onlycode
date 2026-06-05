extends Area2D

#Deze aanpassen voor het goeie aantal kaarten
@export var card_count: int = 40

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	self.input_event.connect(_on_pile_input_event)

### HIER KAN KAART PAKKEN LOGICA MET SERVER
func _on_pile_input_event(viewport: Node, event: InputEvent, shape_idx: int):
	#Check voor muisklik en of knop is ingedrukt
	if event is InputEventMouseButton and event.pressed:
		decrease_counter()

# Haalt 1 kaart van de counter
func decrease_counter():
	if card_count > 0:
		card_count -= 1
	else:
		print("Pile is empty")
