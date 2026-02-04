import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { ChartConfiguration, ChartOptions, Chart, registerables } from 'chart.js';
import { ChartsModule } from '../../../shared/modules/charts.module';
import { UsersService, PaginatedUsersResponse } from '../../../core/services/users.service';
import { SessionsService, PagedResult } from '../../../core/services/sessions.service';

// Register Chart.js components
Chart.register(...registerables);

@Component({
  selector: 'app-dashboard-home',
  standalone: true,
  imports: [CommonModule, RouterModule, ChartsModule],
  templateUrl: './dashboard-home.component.html',
  styleUrl: './dashboard-home.component.scss'
})
export class DashboardHomeComponent implements OnInit {
  private readonly api = `${environment.apiBaseUrl}/api/${environment.apiVersion}`;
  
  stats = {
    totalUsers: 0,
    activeUsers: 0,
    activeSessions: 0,
    recentUsers: 0
  };

  isSystemAdmin = false;
  loading = true;

  // User Growth Trend Chart
  userGrowthChartData: ChartConfiguration<'line'>['data'] = {
    labels: [],
    datasets: [{
      data: [],
      label: 'Users Registered',
      borderColor: '#667eea',
      backgroundColor: 'rgba(102, 126, 234, 0.1)',
      fill: true,
      tension: 0.4
    }]
  };

  userGrowthChartOptions: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'top'
      },
      tooltip: {
        mode: 'index',
        intersect: false
      }
    },
    scales: {
      y: {
        beginAtZero: true,
        ticks: {
          stepSize: 1
        }
      }
    }
  };

  // User Activity Chart (Active vs Inactive)
  userActivityChartData: ChartConfiguration<'pie'>['data'] = {
    labels: ['Active Users', 'Inactive Users'],
    datasets: [{
      data: [0, 0],
      backgroundColor: ['#667eea', '#e2e8f0'],
      borderWidth: 0
    }]
  };

  userActivityChartOptions: ChartOptions<'pie'> = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
        position: 'bottom',
        labels: {
          generateLabels: (chart) => {
            const data = chart.data;
            if (data.labels && data.datasets) {
              const backgroundColor = data.datasets[0].backgroundColor as string[] | undefined;
              return data.labels.map((label, i) => {
                const value = data.datasets[0].data[i] as number;
                const total = (data.datasets[0].data as number[]).reduce((a, b) => a + b, 0);
                const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0';
                const color = backgroundColor && Array.isArray(backgroundColor) ? backgroundColor[i] : '#667eea';
                return {
                  text: `${label}: ${value} (${percentage}%)`,
                  fillStyle: color,
                  strokeStyle: color,
                  lineWidth: 0,
                  hidden: false,
                  index: i
                };
              });
            }
            return [];
          }
        }
      },
      tooltip: {
        callbacks: {
          label: (context) => {
            const label = context.label || '';
            const value = context.parsed || 0;
            const total = context.dataset.data.reduce((a: number, b: number) => a + b, 0);
            const percentage = total > 0 ? ((value / total) * 100).toFixed(1) : '0';
            return `${label}: ${value} (${percentage}%)`;
          }
        }
      }
    }
  };

  constructor(
    private router: Router,
    private http: HttpClient,
    private usersService: UsersService,
    private sessionsService: SessionsService
  ) {}

  ngOnInit(): void {
    // Check if user is System Administrator
    const roles = (localStorage.getItem('roles') || '').split(',').filter(Boolean);
    this.isSystemAdmin = roles.includes('System Administrator');

    // Redirect System Admin to system dashboard
    if (this.isSystemAdmin) {
      this.router.navigate(['/system/dashboard']);
      return;
    }

    // Load dashboard statistics for organization admin
    this.loadStats();
    this.loadUserGrowthTrend();
  }

  loadStats(): void {
    // Load user statistics
    this.usersService.getStatistics().subscribe({
      next: (data) => {
        this.stats.totalUsers = data.total || 0;
        this.stats.activeUsers = data.active || 0;
        this.stats.recentUsers = data.recentlyCreatedUsers || 0;
        
        // Update activity chart
        this.userActivityChartData = {
          labels: ['Active Users', 'Inactive Users'],
          datasets: [{
            data: [data.active || 0, data.inactive || 0],
            backgroundColor: ['#667eea', '#e2e8f0'],
            borderWidth: 0
          }]
        };
        
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading user stats:', error);
        this.loading = false;
      }
    });

    // Load active sessions - get organization sessions and count active ones
    this.sessionsService.getOrganizationSessions(1, 1000).subscribe({
      next: (response: PagedResult<any>) => {
        const activeSessions = (response.items || []).filter((s: any) => s.isActive).length;
        this.stats.activeSessions = activeSessions;
      },
      error: (error: any) => {
        console.error('Error loading sessions:', error);
      }
    });
  }

  loadUserGrowthTrend(): void {
    // Get users with pagination to calculate trend
    const endDate = new Date();
    const startDate = new Date();
    startDate.setMonth(startDate.getMonth() - 12);

    // Calculate monthly user growth
    const months: string[] = [];
    const counts: number[] = [];
    
    for (let i = 11; i >= 0; i--) {
      const date = new Date();
      date.setMonth(date.getMonth() - i);
      months.push(date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' }));
      counts.push(0); // Will be populated from API
    }

    // Load users and calculate monthly distribution
    this.usersService.getData({ page: 1, pageSize: 1000 }).subscribe({
      next: (response: PaginatedUsersResponse) => {
        const users = response.items || [];
        const monthlyCounts = new Array(12).fill(0);

        users.forEach((user) => {
          if (user.createdAtUtc) {
            const createdDate = new Date(user.createdAtUtc);
            const monthsAgo = 11 - Math.floor((endDate.getTime() - createdDate.getTime()) / (1000 * 60 * 60 * 24 * 30));
            if (monthsAgo >= 0 && monthsAgo < 12) {
              monthlyCounts[monthsAgo]++;
            }
          }
        });

        // Calculate cumulative counts
        let cumulative = 0;
        const cumulativeCounts = monthlyCounts.map(count => {
          cumulative += count;
          return cumulative;
        });

        this.userGrowthChartData = {
          labels: months,
          datasets: [{
            data: cumulativeCounts,
            label: 'Total Users',
            borderColor: '#667eea',
            backgroundColor: 'rgba(102, 126, 234, 0.1)',
            fill: true,
            tension: 0.4
          }]
        };
      },
      error: (error: any) => {
        console.error('Error loading user growth trend:', error);
      }
    });
  }
}
