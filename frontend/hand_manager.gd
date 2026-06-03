extends MarginContainer

@export var card_spawner: Button
@export var Hand: HBoxContainer

@onready var Card: PackedScene = preload("res://card.tscn")
var nCards = 1

func spawn_card():
	var Card1 = Card.instantiate()
	if (nCards > 0):
		nCards += 1
	Hand.add_theme_constant_override("separation", (nCards^-2)*2+5)
	Hand.add_child(Card1)

func _on_button_pressed() -> void:
	spawn_card()
