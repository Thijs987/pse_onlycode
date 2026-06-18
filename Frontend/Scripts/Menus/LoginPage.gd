extends Control

# Zet op false bij testen met server ipv local
const USE_LOCAL_MOCK = false 

# Automatically use localhost in Godot Editor, and the actual server for exported builds
var BASE_URL: String = "https://localhost:6969" if OS.has_feature("editor") else "https://codegreen-uva.ddns.net"

const REGISTER_ENDPOINT = "/api/auth/register"
const LOGIN_ENDPOINT = "/api/auth/login"

const MOCK_FILE_PATH = "user://mock_database.json"

# UI REFERENCES - LEFT (REGISTER)
@onready var reg_email_input: LineEdit = $HBoxContainer/LeftRegisterPanel/RegisterVBox/RegEmailInput
@onready var reg_username_input: LineEdit = $HBoxContainer/LeftRegisterPanel/RegisterVBox/RegUserInput
@onready var reg_password_input: LineEdit = $HBoxContainer/LeftRegisterPanel/RegisterVBox/RegPasswordInput
@onready var register_button: Button = $HBoxContainer/LeftRegisterPanel/RegisterVBox/RegisterButton
@onready var register_status: Label = $HBoxContainer/LeftRegisterPanel/RegisterVBox/RegisterStatusLabel

# UI REFERENCES - RIGHT (LOGIN)
@onready var login_email_input: LineEdit = $HBoxContainer/RightLoginPanel/LoginVBox/LoginEmailInput
@onready var login_password_input: LineEdit = $HBoxContainer/RightLoginPanel/LoginVBox/LoginPasswordInput
@onready var login_button: Button = $HBoxContainer/RightLoginPanel/LoginVBox/LoginButton
@onready var login_status: Label = $HBoxContainer/RightLoginPanel/LoginVBox/LoginStatusLabel

# GENERAL NODES
@onready var return_button: Button = $MarginContainer/ReturnButton
@onready var http_request: HTTPRequest = $HTTPRequest

var is_submitting: bool = false

func _ready() -> void:
	register_button.pressed.connect(_on_register_button_pressed)
	login_button.pressed.connect(_on_login_button_pressed)
	return_button.pressed.connect(_on_return_button_pressed)
	
	register_status.text = ""
	login_status.text = ""

# LOGIN
func _on_login_button_pressed() -> void:
	if is_submitting: return
	
	var email = login_email_input.text.strip_edges()
	var password = login_password_input.text
	
	if email == "" or password == "":
		login_status.text = "Please fill in all fields."
		return
		
	set_loading_state(true, "login")
	
	var success: bool
	if USE_LOCAL_MOCK:
		success = await _handle_local_mock_auth(LOGIN_ENDPOINT, {"email": email, "password": password}, login_status)
	else:
		success = await _send_auth_request(LOGIN_ENDPOINT, {"email": email, "password": password}, login_status)
		
	set_loading_state(false, "login")
	
	if success:
		login_status.text = "Login successful!"
		SceneLoader.load_scene("uid://ctined7qq8dh2")

# REGISTER
func _on_register_button_pressed() -> void:
	if is_submitting: return
	
	var email = reg_email_input.text.strip_edges()
	var username = reg_username_input.text.strip_edges()
	var password = reg_password_input.text
	
	if email == "" or username == "" or password == "":
		register_status.text = "Please fill in all fields."
		return
		
	set_loading_state(true, "register")
	
	var register_data = {
		"email": email,
		"username": username,
		"password": password
	}
	
	var success: bool
	if USE_LOCAL_MOCK:
		success = await _handle_local_mock_auth(REGISTER_ENDPOINT, register_data, register_status)
	else:
		success = await _send_auth_request(REGISTER_ENDPOINT, register_data, register_status)
		
	set_loading_state(false, "register")
	
	if success:
		register_status.text = "Registration complete! Please verify your email."
		reg_email_input.clear()
		reg_username_input.clear()
		reg_password_input.clear()

func _on_return_button_pressed() -> void:
	if not is_submitting:
		SceneLoader.load_scene("uid://ctined7qq8dh2")

# --- REAL SERVER NETWORK FUNCTION ---
func _send_auth_request(endpoint: String, data: Dictionary, status_label: Label) -> bool:
	var url = BASE_URL + endpoint
	var headers = ["Content-Type: application/json"]
	var body = JSON.stringify(data)
	
	http_request.set_tls_options(TLSOptions.client_unsafe())
	
	var send_error = http_request.request(url, headers, HTTPClient.METHOD_POST, body)
	if send_error != OK:
		status_label.text = "Network error while connecting."
		return false
		
	var response = await http_request.request_completed
	var response_code = response[1]
	var response_body = response[3].get_string_from_utf8()
	var json_data = JSON.parse_string(response_body)
	
	if response_code == 200:
		if endpoint == LOGIN_ENDPOINT and json_data:
			# Login response contains: { user: {...}, token: "...", expires: "..." }
			if json_data.has("token"):
				var user_data = json_data.get("user", {})
				auth_manager.set_auth(json_data["token"], user_data)
				if json_data.has("expires"):
					auth_manager.set_token_expires(str(json_data["expires"]))
				# Set player ID from user data
				controller.PId = str(user_data.get("username", ""))
			elif json_data.has("username"):
				# Fallback: no JWT configured on server
				controller.PId = str(json_data["username"])
		elif json_data and json_data.has("username"):
			controller.PId = str(json_data["username"])
		return true
	elif response_code == 401 or response_code == 400:
		if json_data and json_data.has("message"):
			status_label.text = json_data["message"]
		elif json_data and json_data.has("Message"):
			status_label.text = json_data["Message"]
		else:
			status_label.text = "Authentication failed (" + str(response_code) + ")."
		return false
	else:
		status_label.text = "Error code: " + str(response_code)
		return false

# --- MOCK DATABASE FUNCTION (LOCAL TESTING) ---
func _handle_local_mock_auth(endpoint: String, data: Dictionary, status_label: Label) -> bool:
	await get_tree().create_timer(0.5).timeout
	
	var local_db = {}
	if FileAccess.file_exists(MOCK_FILE_PATH):
		var file = FileAccess.open(MOCK_FILE_PATH, FileAccess.READ)
		var json_text = file.get_as_text()
		file.close()
		var parsed = JSON.parse_string(json_text)
		if parsed is Dictionary:
			local_db = parsed
			
	if endpoint == REGISTER_ENDPOINT:
		var email = data["email"]
		if local_db.has(email):
			status_label.text = "Email already taken!"
			return false
			
		local_db[email] = {
			"username": data["username"],
			"password": data["password"],
			"id": data["username"]
		}
		
		var file = FileAccess.open(MOCK_FILE_PATH, FileAccess.WRITE)
		file.store_string(JSON.stringify(local_db))
		file.close()
		return true
		
	elif endpoint == LOGIN_ENDPOINT:
		var email = data["email"]
		if not local_db.has(email):
			status_label.text = "Email not found."
			return false
			
		var user_record = local_db[email]
		if user_record["password"] != data["password"]:
			status_label.text = "Wrong password."
			return false
			
		controller.PId = user_record["id"]
		return true
		
	return false

func set_loading_state(busy: bool, mode: String) -> void:
	is_submitting = busy
	register_button.disabled = busy
	login_button.disabled = busy
	return_button.disabled = busy
	
	if busy:
		if mode == "login": login_status.text = "Logging in..."
		if mode == "register": register_status.text = "Creating account..."
