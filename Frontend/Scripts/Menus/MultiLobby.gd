extends Control

@onready var lobby_input: LineEdit = $BrowserView/VBoxContainer/HBoxContainer3/LineEdit
@onready var in_lobby: Label = $InLobbyView/HBoxContainer2/CurrentlyInLobby
@onready var player_list_label: Label = $InLobbyView/PlayerList
@onready var lobby_item_list: ItemList = $BrowserView/LobbyItemList
@onready var refresh_button: Button = $BrowserView/RefreshButton
@onready var error_dialog: AcceptDialog = $BrowserView/ErrorDialog
@onready var browser_view: Control = $BrowserView
@onready var in_lobby_view: Control = $InLobbyView
@onready var leave_lobby_button: Button = $InLobbyView/HBoxContainer6/LeaveLobby

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
	refresh_button.pressed.connect(_on_refresh_lobbies)
	leave_lobby_button.pressed.connect(_on_leave_lobby)
	lobby_item_list.item_selected.connect(_on_lobby_selected)
	controller.message_updated.connect(_on_message)
	controller.lobbies_updated.connect(_on_lobbies_updated)
	controller.lobby_join_failed.connect(_on_lobby_join_failed)
	controller.lobby_left.connect(_on_lobby_left)
	gscws.lobby_joined.connect(_on_lobby_joined_success)
	
	update_views()
	controller.Get_Lobbies()

func _on_lobby_joined_success() -> void:
	in_lobby.text = "Currently in lobby"
	in_lobby_state = true
	update_views()

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
	player_list_label.text = "Current players:\n"
	for i in range(player_count):
		player_list_label.text += player_list[i]

func _on_refresh_lobbies() -> void:
	controller.Get_Lobbies()

func _on_lobbies_updated(lobbies: Array) -> void:
	lobby_item_list.clear()
	for lobby in lobbies:
		var lobby_id = lobby.get("lobbyId", lobby.get("LobbyId", ""))
		var count = lobby.get("playerCount", lobby.get("PlayerCount", 0))
		var capacity = lobby.get("capacity", lobby.get("Capacity", 4))
		lobby_item_list.add_item("[%d/%d] Lobby %s" % [count, capacity, lobby_id])
		# Store the ID in metadata so we can easily retrieve it
		var idx = lobby_item_list.get_item_count() - 1
		lobby_item_list.set_item_metadata(idx, lobby_id)

func _on_lobby_selected(index: int) -> void:
	var lobby_id = lobby_item_list.get_item_metadata(index)
	lobby_input.text = lobby_id

func _on_lobby_join_failed() -> void:
	error_dialog.dialog_text = "Failed to join lobby. It might be full or already started."
	error_dialog.popup_centered()

func _on_leave_lobby() -> void:
	controller.Leave_Lobby()

func _on_lobby_left() -> void:
	in_lobby_state = false
	player_count = 0
	player_list = ["", "", "", ""]
	created_lobby = false
	update_lobby_list()
	update_views()
	controller.Get_Lobbies()

func update_views() -> void:
	browser_view.visible = not in_lobby_state
	in_lobby_view.visible = in_lobby_state
