extends Node2D

signal hovered
signal hovered_away

var movable = true
var is_others
var own_card_id = null
var hand_position
# Dit zet je in het script van de losse KAART (niet in card_logic.gd)
var is_playable = true

func set_playable_visual(playable: bool) -> void:
	is_playable = playable
	if playable:
		modulate = Color(1, 1, 1, 1) # Normale kleur
	else:
		modulate = Color(0.5, 0.5, 0.5, 0.6) # Donkerder/transparanter (blijft wel selecteerbaar voor Trojan)


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if get_parent().has_method("connect_card_signals"):
		get_parent().connect_card_signals(self)

func _on_area_2d_mouse_entered() -> void:
	if movable:
		emit_signal("hovered", self)


func _on_area_2d_mouse_exited() -> void:
	if movable:
		emit_signal("hovered_away", self)

func set_card(card_id):
	own_card_id = card_id
	var sprite = $Sprite2D
	sprite.texture = load("res://Sprites/CardIcons/" + str(card_id) + ".png")
