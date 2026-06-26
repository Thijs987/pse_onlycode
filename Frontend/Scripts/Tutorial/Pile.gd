extends Node2D

@onready var hand_reference = $"../PlayerHand"
@onready var draw_sound := AudioStreamPlayer.new()

const CARD_SCENE = preload("uid://dlb3crw3qdkv2")

var visual_cards = []
var turns = null
var updating_pile := false
var card_count = 40

signal card_drawn()

func _ready():
	add_child(draw_sound)
	draw_sound.stream = preload("res://Sounds/PlayCard.mp3")
	create_visual_pile()

# Draws a card
func draw_card():
	if controller.interaction_disabled:
		return
	draw_sound.play()
	update_visual_pile()
	card_drawn.emit()

#Create the visual cards at the top of the pile
func create_visual_pile():
	# Remove old cards
	for card in visual_cards:
		card.queue_free()

	visual_cards.clear()

	var amount = min(3, card_count)

	for i in range(amount):
		var card = CARD_SCENE.instantiate()

		add_child(card)

		card.set_card("achterkant")
		card.movable = false
		card.is_others = true

		card.get_node("Area2D").monitoring = false
		card.get_node("Area2D").monitorable = false
		card.get_node("Area2D").collision_layer = 0
		card.get_node("Area2D").collision_mask = 0

		setup_draw_pile_card(card, i)

		visual_cards.append(card)

#Set the cards in the correct position
func setup_draw_pile_card(card, index):
	var card_size = card.get_node("Sprite2D").texture.get_size()

	var target_scale = Vector2(
		300.0 / card_size.x,
		450.0 / card_size.y
	)

	card.scale = target_scale

	var pile_pos = $PileArea.global_position

	card.global_position = pile_pos + Vector2(
		index * 4,
		index * 4
	)

	card.rotation_degrees = randf_range(-4.0, 4.0)

	card.z_index = index

#Update how the pile looks. Adds the new card visual on the bottom
func update_visual_pile():
	if updating_pile:
		return

	updating_pile = true
	var wanted_cards = min(3, card_count)

	# Remove cards if needed
	while visual_cards.size() > wanted_cards:
		var card = visual_cards.pop_back()

		var tween = get_tree().create_tween()

		tween.parallel().tween_property(
			card,
			"scale",
			Vector2.ZERO,
			0.15
		)

		await tween.finished

		card.queue_free()
	updating_pile = false

	while visual_cards.size() < wanted_cards:
		var card = CARD_SCENE.instantiate()

		add_child(card)

		card.set_card("achterkant")
		card.movable = false
		card.is_others = true

		card.get_node("Area2D").monitoring = false
		card.get_node("Area2D").monitorable = false
		card.get_node("Area2D").collision_layer = 0
		card.get_node("Area2D").collision_mask = 0

		setup_draw_pile_card(card, visual_cards.size())

		visual_cards.append(card)

	# Reposition stack nicely
	for i in range(visual_cards.size()):
		setup_draw_pile_card(visual_cards[i], i)
