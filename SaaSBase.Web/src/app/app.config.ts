import { ApplicationConfig } from '@angular/core';
import { provideRouter, RouteReuseStrategy, withInMemoryScrolling } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimations } from '@angular/platform-browser/animations';
import { authInterceptor } from './core/auth/auth.interceptor';
import { CustomRouteReuseStrategy } from './core/route-reuse-strategy';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(
      routes,
      withInMemoryScrolling({
        scrollPositionRestoration: 'top',
        anchorScrolling: 'enabled'
      })
    ),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimations(),
    { provide: RouteReuseStrategy, useClass: CustomRouteReuseStrategy }
  ]
};
