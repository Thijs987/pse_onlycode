extends Control

@onready var panel = $Panel
@onready var label = $Panel/Label

func show_tooltip(card_id):
	tooltip_text(card_id)
	label.reset_size()
	panel.z_index = 1000
	panel.size = label.size
	panel.show()

func hide_tooltip():
	panel.hide()

func tooltip_text(card_id):
	match card_id:
		"blue":
			label.text = "This card is unplayable\nLose it by using cards their effects"
		"cm":
			label.text = "End your turn without drawing a card"
		"ddos":
			label.text = "End your turn without drawing a card\nNext player has to take two turns"
		"err":
			label.text = "This card is unplayable\nLose it by using cards their effects"
		"goto":
			label.text = "Give the top card of the drawing pile to a player\nYou can use this card as a joker for any other bad coding habit"
		"imp":
			label.text = "Remove a card from your hand\nDoes nothing when your hand is empty\nShould always be played first"
		"inf":
			label.text = "Give the top card of the drawing pile to a player\nYou need 2 of this card to play them"
		"merge":
			label.text = "This card is unplayable\nLose it by using cards their effects"
		"miracle":
			label.text = "Shuffle the cards in the drawing pile"
		"nocom":
			label.text = "Give the top card of the drawing pile to a player\nYou need 2 of this card to play them"
		"os":
			label.text = "Draw and look at the bottom card of the drawing pile\nDecide wether to keep it or put it on top of the drawing pile"
		"sql":
			label.text = "End your turn without drawing a card\nDecide which player has to take two turns"
		"test":
			label.text = "Oops, this card shouldn't be here\nThis card is for tests!"
		"trojan":
			label.text = "Choose a player to give a card from your hand"
		"vibe":
			label.text = "Give the top card of the drawing pile to a player\nYou need 2 of this card to play them"
		_:
			label.text = "Error: this card should not exist"
