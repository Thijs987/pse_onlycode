extends Control

# UI elements
@onready var login_button: Button = $ButtonContainer/LoginContainer/LoginLayout/LoginButton
@onready var welcome_label: Label = $ButtonContainer/LoginContainer/LoginLayout/WelcomeLabel
@onready var mute_button: Button = $ButtonContainer/MuteButtonContainer/HBoxContainer/MuteButton
@onready var background: TextureRect = $Background

@export var multi_lobby: StringName = &""
@export var main_menu_buttons: MarginContainer
@export var first_menu_screen: MarginContainer
@export var press_to_start: TextureRect
@export var press_start_animation: AnimationPlayer
@export var movement_ease_curve: Curve

var bg_start_pos
var in_main_menu: bool = false

# TEMP FOR TESTING
var is_logged_in: bool = false
var is_muted: bool = false

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	press_start_animation.play("PressStartAnimation")
	bg_start_pos = background.position
	
	login_button.pressed.connect(_on_login_pressed)
	mute_button.pressed.connect(_on_mute_pressed)
	
	check_login_status()

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	move_background()
	
func _input(event: InputEvent) -> void:
	if !in_main_menu and event.is_pressed():
			press_start_animation.play("PressStartPressed")
			await press_start_animation.animation_finished
			main_menu_buttons.visible = true
			show_buttons_menu(first_menu_screen)
			in_main_menu = true

func show_buttons_menu(TitleContainer):
	var tween = create_tween()
	var end_pos = TitleContainer.position + Vector2(0, -80)
	
	tween.tween_method(
		func(progress):
			var curve_value = movement_ease_curve.sample(progress)
			TitleContainer.position = lerp(TitleContainer.position, end_pos, curve_value), 0.0, 1.0, 4
	)

# Creates the moving background
func move_background() -> void:
	background.position.x -= 0.25
	background.position.y -= 0.5
	
	if background.position.x == bg_start_pos.x - 80:
		background.position = bg_start_pos

# Play button (Not yet with the right path!!!)
func _on_play_button_pressed() -> void:
	if controller.PId != "":
		SceneLoader.load_scene(multi_lobby)

# Quit button
func _on_quit_button_pressed() -> void:
	get_tree().quit()

# Login check and visibility of the button
func check_login_status() -> void:
	if auth_manager.is_authenticated:
		login_button.visible = false
		welcome_label.bbcode_enabled = false  # Disable BBCode to prevent injection
		welcome_label.text = "Welcome " + auth_manager.username
		welcome_label.visible = true
	else:
		login_button.visible = true
		welcome_label.visible = false

# Button to login page (Not yet with the right path!!!)
func _on_login_pressed() -> void:
	# Need to change this to real login page location
	SceneLoader.load_scene("uid://bwt1f1f61krv")

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
