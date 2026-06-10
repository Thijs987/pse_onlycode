extends Control

@export var game_scene: StringName = &""
@export var start_lobby_button: Button


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	start_lobby_button.pressed.connect(_on_start_lobby)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _on_start_lobby() -> void:
	SceneLoader.load_scene(game_scene)
