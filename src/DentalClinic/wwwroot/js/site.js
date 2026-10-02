(function () {
  "use strict";

  // Sticky header shadow
  var header = document.getElementById("header");
  var onScroll = function () { header && header.classList.toggle("scrolled", window.scrollY > 8); };
  onScroll();
  window.addEventListener("scroll", onScroll, { passive: true });

  // Mobile menu
  var burger = document.getElementById("burger");
  var nav = document.getElementById("nav");
  if (burger && nav) {
    burger.addEventListener("click", function () {
      var open = nav.classList.toggle("open");
      burger.setAttribute("aria-expanded", open ? "true" : "false");
    });
  }

  // Reveal on scroll
  var items = document.querySelectorAll(".reveal");
  if ("IntersectionObserver" in window) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); }
      });
    }, { threshold: 0.12 });
    items.forEach(function (el) { io.observe(el); });
  } else {
    items.forEach(function (el) { el.classList.add("in"); });
  }

  // Client-side category filter (services page)
  document.querySelectorAll("[data-filter]").forEach(function (chip) {
    chip.addEventListener("click", function () {
      var value = chip.getAttribute("data-filter");
      document.querySelectorAll("[data-filter]").forEach(function (c) { c.classList.toggle("active", c === chip); });
      document.querySelectorAll("[data-category]").forEach(function (card) {
        card.hidden = value !== "all" && card.getAttribute("data-category") !== value;
      });
    });
  });

  // Count-up numbers
  document.querySelectorAll("[data-count]").forEach(function (el) {
    var target = parseFloat(el.getAttribute("data-count"));
    var decimals = (el.getAttribute("data-count").split(".")[1] || "").length;
    var suffix = el.getAttribute("data-suffix") || "";
    var started = false;
    var run = function () {
      if (started) return; started = true;
      var t0 = performance.now(), dur = 1200;
      var step = function (t) {
        var p = Math.min(1, (t - t0) / dur), eased = 1 - Math.pow(1 - p, 3);
        el.textContent = (target * eased).toFixed(decimals).replace(".", ",") + suffix;
        if (p < 1) requestAnimationFrame(step);
      };
      requestAnimationFrame(step);
    };
    if ("IntersectionObserver" in window) {
      new IntersectionObserver(function (es, o) { if (es[0].isIntersecting) { run(); o.disconnect(); } }).observe(el);
    } else { run(); }
  });

  // Demo-account quick fill (login page)
  document.querySelectorAll("[data-demo-email]").forEach(function (btn) {
    btn.addEventListener("click", function () {
      var email = document.querySelector("input[name=Email]");
      var pass = document.querySelector("input[name=Password]");
      if (email && pass) { email.value = btn.getAttribute("data-demo-email"); pass.value = "Demo123!"; email.form && email.form.submit(); }
    });
  });

  // Confirm dialogs for destructive actions
  document.querySelectorAll("form[data-confirm]").forEach(function (f) {
    f.addEventListener("submit", function (e) { if (!window.confirm(f.getAttribute("data-confirm"))) e.preventDefault(); });
  });
})();
