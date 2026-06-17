extends Control

@onready var panel = $Panel
@onready var label = $Panel/Label

func show_tooltip(card_id):
	tooltip_text(card_id)
	panel.z_index = 1000
	panel.size = label.size
	panel.show()

func hide_tooltip():
	panel.hide()

func tooltip_text(card_id):
	match card_id:
		"blue":
			label.text = "You can't play this card"
		"cm":
			label.text = "Skip drawing a card"
		"ddos":
			label.text = "Next player plays 2 turns\nYou skip drawing a card"
		"err":
			label.text = "You can't play this card"
		"goto":
			label.text = "Play 2 of this card to give a player the top card of the drawing pile\nCan also be used as a joker with the other bad coding habits"
		"inf":
			label.text = "Play 2 of this card to give a player the top card of the drawing pile"
		"merge":
			label.text = "You can't play this card"
		"miracle":
			label.text = "Shuffle the drawing pile"
		"nocom":
			label.text = "Play 2 of this card to give a player the top card of the drawing pile"
		"os":
			label.text = "Draw and see the bottom card of the draw pile\nDecide whether to keep the card or put it on top"
		"sql":
			label.text = "Decide which player plays 2 turns\nYou skip drawing a card"
		"test":
			label.text = "Oops, this card shouldn't be here\nThis card is for tests!"
		"trojan":
			label.text = "Give a card in your hand to another player"
		"vibe":
			label.text = "Play 2 of this card to give a player the top card of the drawing pile"
		_:
			label.text = "Mystery card: text can't be loaded"
