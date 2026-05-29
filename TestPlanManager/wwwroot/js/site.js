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

		// Priority: place the menu above the button so the button/ellipsis stays unobscured.
		let top = triggerRect.top - menuRect.height - 4;
		let left = triggerRect.right - menuRect.width;

		if (left < 8) {
			left = 8;
		}

		if (left + menuRect.width > viewportWidth - 8) {
			left = Math.max(8, triggerRect.left);
		}

		if (top < 8) {
			// no space above: fallback below the button
			top = triggerRect.bottom + 4;
		}

		if (top + menuRect.height > viewportHeight - 8) {
			// still not enough space: force within viewport
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

	// Global delete/confirm handler: listens for form submissions and prompts when forms
	// are marked with `data-confirm-message` or when the submitter looks like a delete action.
	document.addEventListener('submit', function (event) {
		var form = event.target;
		if (!(form instanceof HTMLFormElement)) {
			return;
		}

		var submitter = event.submitter;
		var confirmMessage = form.dataset.confirmMessage || form.dataset.deleteConfirmMessage;
		var looksLikeDelete = false;

		if (submitter) {
			var submitText = ((submitter.textContent || '') + ' ' + (submitter.title || '') + ' ' + (submitter.getAttribute('aria-label') || '')).toLowerCase();
			looksLikeDelete = submitText.includes('delete') || submitText.includes('remove') || submitText.includes('trash');
		}

		if (!confirmMessage && !looksLikeDelete) {
			return;
		}

		if (!confirmMessage) {
			confirmMessage = 'Are you sure you want to delete this? This action cannot be undone.';
		}

		if (!window.confirm(confirmMessage)) {
			event.preventDefault();
		}
	});

	// Delegated handlers for data-href navigation and custom actions (avoids inline onclick)
	document.addEventListener('click', function (event) {
		var el = event.target;
		// Walk up to find an element with data-stop-propagation
		var stop = el.closest && el.closest('[data-stop-propagation]');
		if (stop) {
			// If the clicked element is inside an area that stops propagation for row clicks,
			// allow the click to proceed normally (e.g., opening dropdowns) and don't trigger navigation.
			return;
		}

		// Handle prepare-make-template buttons
		var prep = el.closest && el.closest('.js-prepare-make-template');
		if (prep) {
			// Call the function defined in page-scoped inline script (it has a nonce so it runs)
			try {
				if (typeof window.prepareMakeTemplateModal === 'function') {
					window.prepareMakeTemplateModal(prep);
				}
			} catch (e) { }
			return;
		}

		// Find nearest ancestor row with data-href
		var row = el.closest && el.closest('[data-href]');
		if (row) {
			// Ignore clicks on interactive controls inside the row
			var tag = el.tagName && el.tagName.toLowerCase();
			if (tag === 'a' || tag === 'button' || tag === 'input' || el.closest && el.closest('a,button,input')) {
				return;
			}
			var href = row.getAttribute('data-href');
			if (href) {
				window.location = href;
			}
		}
	});
