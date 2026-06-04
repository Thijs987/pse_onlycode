extends Node


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	GSCHTTP.Create_Lobby("Player_1")


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
