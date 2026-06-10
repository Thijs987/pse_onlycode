extends Node2D

signal hovered
signal hovered_away

var movable = true
var hand_position

var card_textures = {
	"goto": preload("res://CardIcons/Defuse.png"),
}
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if get_parent().has_method("connect_card_signals"):
		get_parent().connect_card_signals(self)

func set_card(card_id):
	if card_id in card_textures:
		$Sprite2D.texture = card_textures[card_id]

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass

func _on_area_2d_mouse_entered() -> void:
	if movable:
		emit_signal("hovered", self)


func _on_area_2d_mouse_exited() -> void:
	if movable:
		emit_signal("hovered_away", self)
