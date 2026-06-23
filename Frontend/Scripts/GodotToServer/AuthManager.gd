extends Node

# Global authentication manager (autoload singleton).
# Stores the JWT token after login and provides it for HTTP/WebSocket requests.

var jwt_token: String = ""
var user_id: String = ""
var username: String = ""
var email: String = ""
var token_expires: String = ""

var is_authenticated: bool:
	get:
		return jwt_token != ""


func set_auth(token: String, user_data: Dictionary) -> void:
	jwt_token = token
	user_id = str(user_data.get("id", ""))
	username = str(user_data.get("username", ""))
	email = str(user_data.get("email", ""))
	print("[AuthManager] Authenticated as: ", username, " (", user_id, ")")


func set_token_expires(expires: String) -> void:
	token_expires = expires


func get_auth_headers() -> PackedStringArray:
	## Returns headers with JWT Bearer token included.
	## Use this for all authenticated HTTP requests.
	var headers: PackedStringArray = ["Content-Type: application/json"]
	if jwt_token != "":
		headers.append("Authorization: Bearer " + jwt_token)
	return headers


func get_ws_url_with_auth(base_url: String) -> String:
	## Appends the JWT token as a query parameter for WebSocket connections.
	## WebSocket API doesn't support custom headers, so we pass it as a query param.
	if jwt_token != "":
		if "?" in base_url:
			return base_url + "&access_token=" + jwt_token
		else:
			return base_url + "?access_token=" + jwt_token
	return base_url


func clear_auth() -> void:
	jwt_token = ""
	user_id = ""
	username = ""
	email = ""
	token_expires = ""
	print("[AuthManager] Session cleared.")
