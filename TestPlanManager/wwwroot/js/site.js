document.addEventListener("keydown", function (event) {
	if (event.key === "F7") {
		event.preventDefault();
	}
});

(function () {
	let activeDropdown = null;
	let activeTrigger = null;
	let activeOriginalParent = null;
	let activeNextSibling = null;

	function closeActiveDropdown() {
		if (!activeDropdown || !activeTrigger) {
			return;
		}

		activeDropdown.classList.remove("floating-menu-open", "show");
		activeDropdown.style.display = "none";
		activeDropdown.setAttribute("aria-hidden", "true");
		activeTrigger.setAttribute("aria-expanded", "false");

		if (activeOriginalParent) {
			if (activeNextSibling && activeNextSibling.parentNode === activeOriginalParent) {
				activeOriginalParent.insertBefore(activeDropdown, activeNextSibling);
			} else {
				activeOriginalParent.appendChild(activeDropdown);
			}
		}

		activeDropdown = null;
		activeTrigger = null;
		activeOriginalParent = null;
		activeNextSibling = null;
	}

	function calculateMenuPosition(trigger, menu) {
		const triggerRect = trigger.getBoundingClientRect();
		const menuRect = menu.getBoundingClientRect();
		const viewportWidth = window.innerWidth;
		const viewportHeight = window.innerHeight;

		const zone = document.querySelector(".floating-menu-zone");
		if (zone) {
			const zoneRect = zone.getBoundingClientRect();
			const topWithinZone = zoneRect.top + 8;
			const leftWithinZone = Math.max(zoneRect.left + 8, Math.min(zoneRect.right - menuRect.width - 8, viewportWidth - menuRect.width - 8));
			menu.style.top = `${topWithinZone}px`;
			menu.style.left = `${leftWithinZone}px`;
			return;
		}

		// Prioriteit: menu boven de knop zetten zodat de knop / drie puntjes vrij blijft.
		let top = triggerRect.top - menuRect.height - 4;
		let left = triggerRect.right - menuRect.width;

		if (left < 8) {
			left = 8;
		}

		if (left + menuRect.width > viewportWidth - 8) {
			left = Math.max(8, triggerRect.left);
		}

		if (top < 8) {
			// geen ruimte boven: fallback onder de knop
			top = triggerRect.bottom + 4;
		}

		if (top + menuRect.height > viewportHeight - 8) {
			// nog altijd niet genoeg ruimte, dwing binnen viewport
			top = Math.max(8, viewportHeight - menuRect.height - 8);
		}

		menu.style.top = `${top}px`;
		menu.style.left = `${left}px`;
	}


	function openMenu(trigger, menu) {
		if (activeDropdown && activeTrigger === trigger) {
			closeActiveDropdown();
			return;
		}

		closeActiveDropdown();

		activeDropdown = menu;
		activeTrigger = trigger;
		activeOriginalParent = menu.parentNode;
		activeNextSibling = menu.nextElementSibling;

		document.body.appendChild(menu);
		menu.classList.add("floating-menu-open", "show");
		menu.style.display = "block";
		menu.style.position = "fixed";
		menu.setAttribute("aria-hidden", "false");
		trigger.setAttribute("aria-expanded", "true");

		calculateMenuPosition(trigger, menu);
	}

	function onDocumentClick(event) {
		if (!activeDropdown || !activeTrigger) {
			return;
		}

		if (activeDropdown.contains(event.target) || activeTrigger.contains(event.target)) {
			return;
		}

		closeActiveDropdown();
	}

	function onDocumentKeyDown(event) {
		if (event.key === "Escape") {
			closeActiveDropdown();
		}
	}

	function onScrollOrResize() {
		if (activeDropdown && activeTrigger) {
			calculateMenuPosition(activeTrigger, activeDropdown);
		}
	}

	document.querySelectorAll(".floating-menu-toggle").forEach(button => {
		button.addEventListener("click", function (event) {
			event.preventDefault();
			event.stopPropagation();
			const container = this.closest(".floating-dropdown");
			if (!container) return;
			const menu = container.querySelector(".floating-dropdown-menu");
			if (!menu) return;
			openMenu(this, menu);
		});
	});

	document.addEventListener("click", onDocumentClick);
	document.addEventListener("keydown", onDocumentKeyDown);
	window.addEventListener("resize", onScrollOrResize);
	window.addEventListener("scroll", onScrollOrResize, true);
})();
