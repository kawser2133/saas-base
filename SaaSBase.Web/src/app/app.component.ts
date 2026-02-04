import { Component, OnInit, OnDestroy } from '@angular/core';
import { RouterOutlet, Router, NavigationEnd } from '@angular/router';
import { filter, Subscription } from 'rxjs';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'Build Enterprise SaaS Faster';
  private routerSubscription?: Subscription;

  constructor(private readonly router: Router) {}

  ngOnInit(): void {
    // Scroll to top on route change for all pages (especially public pages)
    this.routerSubscription = this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe(() => {
      this.scrollToTop();
    });
  }

  ngOnDestroy(): void {
    if (this.routerSubscription) {
      this.routerSubscription.unsubscribe();
    }
  }

  private scrollToTop(): void {
    // Scroll window to top
    window.scrollTo({
      top: 0,
      behavior: 'smooth'
    });

    // Also try to scroll any content wrapper (for main layout)
    const contentWrapper = document.querySelector('.content-wrapper');
    if (contentWrapper) {
      contentWrapper.scrollTo({
        top: 0,
        behavior: 'smooth'
      });
    }
  }
}
