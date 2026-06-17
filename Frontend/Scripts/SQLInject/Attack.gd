extends Control

signal target_selected(player_id)
const ATTACK_BUTTON_TEMPLATE = preload("res://Scenes/Attack_button.tscn")

var enemies: Array = []

@onready var player_container = $HBoxContainer

func _ready() -> void:
	for child in player_container.get_children():
		child.queue_free()
	
	for enemy in enemies:
		var new_box = ATTACK_BUTTON_TEMPLATE.instantiate()
		player_container.add_child(new_box)
		new_box.setup_box(enemy)
		new_box.button_clicked.connect(func(gekozen_id):
			target_selected.emit(gekozen_id)
			queue_free()
		)

func setup_targets(enemies_list: Array) -> void:
	enemies = enemies_list
