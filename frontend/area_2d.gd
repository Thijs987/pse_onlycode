extends Area2D

# Deze aanpassen voor het goeie aantal kaarten
@export var card_count: int = 40

# Maakt CounterLabel node
@onready var counter_label: Label = $CounterLabel

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	self.input_event.connect(_on_pile_input_event)
	update_card_text()

func _on_pile_input_event(viewport: Node, event: InputEvent, shape_idx: int):
	# Check voor muisklik en of knop is ingedrukt
	if event is InputEventMouseButton and event.pressed:
		# Laat card count getal zien
		decrease_counter()

# Haalt 1 kaart van de counter
func decrease_counter():
	if card_count > 0:
		card_count -= 1
		# Past card count getal aan
		update_card_text()
	else:
		print("Pile is empty")

# Past card count getal aan
func update_card_text():
	counter_label.text = str(card_count)
