extends Control

# UI elements
@onready var login_button: Button = $TopLeftContainer/LoginLayout/LoginButton
@onready var welcome_label: Label = $TopLeftContainer/LoginLayout/WelcomeLabel
@onready var mute_button: Button = $TopRightContainer/HBoxContainer/MuteButton

@export var multi_lobby: StringName = &""

# TEMP FOR TESTING
var is_logged_in: bool = false
var is_muted: bool = false


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	login_button.pressed.connect(_on_login_pressed)
	mute_button.pressed.connect(_on_mute_pressed)
	
	check_login_status()

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass

# Play button (Not yet with the right path!!!)
func _on_play_button_pressed() -> void:
	# Need to change this to real lobby page location
	controller.Create_Lobby("Player_1")
	SceneLoader.load_scene(multi_lobby)

# Quit button
func _on_quit_button_pressed() -> void:
	get_tree().quit()

# Login check and visibility of the button
func check_login_status() -> void:
	if is_logged_in:
		login_button.visible = false
		welcome_label.text = "Welcome [Name]"
		welcome_label.visible = true
	else:
		login_button.visible = true
		welcome_label.visible = false

# Button to login page (Not yet with the right path!!!)
func _on_login_pressed() -> void:
	# Need to change this to real login page location
	get_tree().change_scene_to_file("res://LoginPage.tscn")

	# Test to check logic
	# is_logged_in = true
	# check_login_status()

# Mute function
func _on_mute_pressed() -> void:
	is_muted = !is_muted

	var master_bus_index = AudioServer.get_bus_index("Master")
	AudioServer.set_bus_mute(master_bus_index, is_muted)

	if is_muted:
		mute_button.text = "Unmute"
	else:
		mute_button.text = "Mute"
	
	#THIS IS TO GET THE LOBBIES. SHOULD CREATE ANOTHER BUTTON FOR THIS
	controller.Get_Lobbies()
	await get_tree().create_timer(1.0).timeout
	print(controller.Active_Lobbies)
