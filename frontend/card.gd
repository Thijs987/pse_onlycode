extends Node2D

signal hovered
signal hovered_away

var movable = true

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	get_parent().connect_card_signals(self)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass

func _on_area_2d_mouse_entered() -> void:
	if movable:
		emit_signal("hovered", self)


func _on_area_2d_mouse_exited() -> void:
	if movable:
		emit_signal("hovered_away", self)
