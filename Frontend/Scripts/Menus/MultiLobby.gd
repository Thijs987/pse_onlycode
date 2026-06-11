extends Control

@onready var lobby_input: LineEdit = $VBoxContainer/HBoxContainer3/LineEdit
@onready var in_lobby: Label = $VBoxContainer/HBoxContainer2/CurrentlyInLobby
@onready var lobby_list: Label = $LobbyList

@export var game_scene: StringName = &""
@export var create_lobby_button: Button
@export var join_lobby_button: Button
@export var start_lobby_button: Button

var player_list = ["", "", "", ""]
var player_count = 0
var match_started = false
var lobby_id

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	create_lobby_button.pressed.connect(_on_create_lobby)
	join_lobby_button.pressed.connect(_on_join_lobby)
	start_lobby_button.pressed.connect(_on_start_lobby)
	controller.message_updated.connect(_on_message)

func _on_message(msg):
	if msg["action"] == "MATCH_STARTED" and match_started == false:
		SceneLoader.load_scene(game_scene)
	if msg["action"] == "PLAYER_JOINED":
		if msg["playerId"] == controller.PId:
			player_list[player_count] = controller.PId + "\n"
			player_count += 1
			update_lobby_list()
		else:
			print("Another player joined")
			player_list[player_count] = msg["playerId"] + "\n"
			player_count += 1
			update_lobby_list()
	elif msg["action"] == "ERROR":
		if msg["playerId"] == controller.PId:
			print("ik poep in mijn broek")
		

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _on_create_lobby() -> void:
	controller.Create_Lobby(controller.PId)
	in_lobby.text = "Currently in lobby"
	
	match_started = true


func _on_join_lobby() -> void:
	lobby_id = lobby_input.text
	if lobby_id:
		controller.Join_Lobby(lobby_id, controller.PId)
	
	in_lobby.text = "Currently in lobby"
	match_started = true

func _on_start_lobby():
	controller.Start_Match(controller.PId)
	SceneLoader.load_scene(game_scene)
	

func update_lobby_list() -> void:
	lobby_list.text = "Current players:\n"
	for i in range(player_count):
		lobby_list.text += player_list[i]
