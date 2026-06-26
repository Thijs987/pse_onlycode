extends AudioStreamPlayer

const menu = preload("res://Songs/Menu.mp3")
const game = preload("res://Songs/Game.mp3")

var volume = 0.0

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	stream = menu
	volume_db = volume
	play()

func play_game_music() -> void:
	if stream == game:
		return
	stream = game
	play()

func play_menu_music() -> void:
	if stream == menu:
		return
	stream = menu
	play()
	
func set_volume(volume: float) -> void:
	volume_db = volume
	
func get_volume() -> float:
	return volume_db
