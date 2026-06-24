extends Node2D

@onready var pile_location = $DiscardPileLocation
@onready var play_sound := AudioStreamPlayer.new()

var discarded_cards = []


func _ready():
	add_child(play_sound)
	play_sound.stream = preload("res://Sounds/PlayCard.mp3")
	gscws.card_played.connect(_on_card_played)
	

func _on_card_played(player_id, card_id):
	play_sound.play()
	var card_scene = preload("uid://dlb3crw3qdkv2")
	var card = card_scene.instantiate()

	add_child(card)
	
	print("Discardpile _on" + card_id)

	card.set_card(card_id)
	card.movable = false

	card.get_node("Area2D").monitoring = false
	card.get_node("Area2D").monitorable = false
	
	Add_Card(card)

func Add_Card(card):
	discarded_cards.append(card)

	var target_size = Vector2(300, 450)
	var card_size = card.get_node("Sprite2D").texture.get_size()

	var target_scale = Vector2(
		target_size.x / card_size.x,
		target_size.y / card_size.y
	)

	var target_position = pile_location.global_position + Vector2(
		randi_range(-5, 5),
		randi_range(-5, 5)
	)

	var target_rotation = randi_range(-8, 8)

	card.global_position = Vector2(
		pile_location.global_position.x,
		0
	)

	card.scale = Vector2(1.0, 1.0)
	card.rotation_degrees = -30
	var tween = get_tree().create_tween()

	tween.set_trans(Tween.TRANS_LINEAR)
	tween.parallel().tween_property(
		card,
		"global_position",
		target_position,
		0.25
	)

	tween.parallel().tween_property(
		card,
		"scale",
		target_scale,
		0.25
	)

	tween.parallel().tween_property(
		card,
		"rotation_degrees",
		target_rotation,
		0.25
	)

	card.z_index = discarded_cards.size()
