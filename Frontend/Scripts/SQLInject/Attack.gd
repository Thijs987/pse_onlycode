extends Control

signal target_selected(player_id)
const ATTACK_BUTTON_TEMPLATE = preload("res://Scenes/Attack_button.tscn")

# We slaan de vijanden hier tijdelijk op
var enemies: Array = []

@onready var player_container = $HBoxContainer

# Verplaats de logica naar _ready(), zodat Godot dit PAS uitvoert 
# als de hele scène en alle nodes (zoals HBoxContainer) perfect geladen zijn!
func _ready() -> void:
	# Maak de container eerst leeg
	for child in player_container.get_children():
		child.queue_free()
	
	# Maak nu pas de knoppen aan voor de vijanden die zijn doorgegeven
	for enemy in enemies:
		var new_box = ATTACK_BUTTON_TEMPLATE.instantiate()
		player_container.add_child(new_box)
		new_box.setup_box(enemy)
		new_box.button_clicked.connect(func(gekozen_id):
			target_selected.emit(gekozen_id)
			queue_free()
		)

# Deze functie geeft nu alleen nog maar de lijst door vóórdat add_child wordt aangeroepen
func setup_targets(enemies_list: Array) -> void:
	enemies = enemies_list
