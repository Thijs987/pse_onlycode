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
var in_lobby_state = false
var created_lobby = false

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	create_lobby_button.pressed.connect(_on_create_lobby)
	join_lobby_button.pressed.connect(_on_join_lobby)
	start_lobby_button.pressed.connect(_on_start_lobby)
	controller.message_updated.connect(_on_message)
	gscws.lobby_joined.connect(_on_lobby_joined_success)

func _on_lobby_joined_success() -> void:
	in_lobby.text = "Currently in lobby"
	in_lobby_state = true

func _on_message(msg):
	if msg["action"] == "MATCH_STARTED" and match_started == false:
		match_started = true
		SceneLoader.load_scene(game_scene)
	elif msg["action"] == "PLAYER_JOINED":
		if msg["playerId"] == controller.PId:
			player_list[player_count] = controller.PId + "\n"
			player_count += 1
			update_lobby_list()
		else:
			print("Another player joined")
			player_list[player_count] = msg["playerId"] + "\n"
			player_count += 1
			update_lobby_list()
	elif msg["action"] == "PLAYER_LEFT":
		var p_id = msg["playerId"]
		for i in range(player_count):
			if player_list[i].strip_edges() == p_id:
				player_list.remove_at(i)
				player_list.append("")
				player_count -= 1
				update_lobby_list()
				break
	elif msg["action"] == "ERROR":
		if msg["playerId"] == controller.PId:
			print("ik poep in mijn broek")
		

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _on_create_lobby() -> void:
	if in_lobby_state == false:
		controller.Create_Lobby(controller.PId)
		created_lobby = true


func _on_join_lobby() -> void:
	if in_lobby_state == false:
		lobby_id = lobby_input.text
		if lobby_id:
			controller.Join_Lobby(lobby_id, controller.PId)

func _on_start_lobby():
	if player_count > 1 and created_lobby == true:
		controller.Start_Match(controller.PId)
	

func update_lobby_list() -> void:
	lobby_list.text = "Current players:\n"
	for i in range(player_count):
		lobby_list.text += player_list[i]
