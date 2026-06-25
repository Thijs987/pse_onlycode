extends ColorRect


func setAlpha (alpha: float) -> void:
	material.set("shader_parameter/alpha", alpha)
