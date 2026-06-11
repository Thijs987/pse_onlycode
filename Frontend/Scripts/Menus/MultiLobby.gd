extends Control

@export var game_scene: StringName = &""
@export var start_lobby_button: Button

var match_started = false

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	start_lobby_button.pressed.connect(_on_start_lobby)
	controller.message_updated.connect(_on_message)

func _on_message(msg):
	if msg["action"] == "MATCH_STARTED" and match_started == false:
		SceneLoader.load_scene(game_scene)

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _on_start_lobby() -> void:
	match_started = true
	controller.Start_Match(controller.PId)
	SceneLoader.load_scene(game_scene)
