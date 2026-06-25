extends Node

# Autoload singleton that plays a click sound whenever any button is pressed.
# It listens for nodes entering the scene tree and hooks the `pressed` signal of
# every BaseButton (Button, CheckBox, CheckButton, etc.) automatically, so new
# scenes and dynamically spawned buttons are covered without any extra wiring.

@onready var click_player := AudioStreamPlayer.new()


func _ready() -> void:
	add_child(click_player)
	click_player.stream = preload("res://Sounds/click.mp3")

	# Connect buttons that get added from now on (the main scene loads after
	# autoloads, so this covers the whole game including the very first screen).
	get_tree().node_added.connect(_on_node_added)


func _on_node_added(node: Node) -> void:
	if node is BaseButton and not node.pressed.is_connected(_play_click):
		node.pressed.connect(_play_click)


func _play_click() -> void:
	click_player.play()
