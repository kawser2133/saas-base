import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UserProfileService, UserProfile, UpdateProfileRequest } from '../../core/services/user-profile.service';
import { NotificationService } from '../../shared/services/notification.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.scss']
})
export class ProfileComponent implements OnInit {
  profile: UserProfile | null = null;
  edit: UpdateProfileRequest = {
    firstName: '',
    lastName: '',
    email: '',
    phoneNumber: '',
    avatar: '',
    employeeId: '',
    department: '',
    jobTitle: '',
    location: '',
    notes: '',
    preferences: { theme: 'light', language: 'en', notifications: { email: true, push: true, sms: false } }
  };
  saving = false;
  uploadingAvatar = false;

  // Dropdown options loaded from API
  locations: string[] = [];
  departments: string[] = [];
  positions: string[] = [];



  constructor(
    private userProfileService: UserProfileService, 
    private notifications: NotificationService
  ) {}

  ngOnInit(): void {
    const userId = localStorage.getItem('userId');
    if (!userId) return;
    
    this.userProfileService.getProfile(userId).subscribe({
      next: (p) => {
        this.profile = p;
        this.edit = {
          firstName: p.firstName,
          lastName: p.lastName,
          email: p.email,
          phoneNumber: p.phoneNumber,
          avatar: p.avatar,
          employeeId: p.employeeId,
          department: p.department,
          jobTitle: p.jobTitle,
          location: p.location,
          notes: p.notes,
          preferences: p.preferences
        } as UpdateProfileRequest;
      }
    });

    // Load dropdown options from Users API
    this.userProfileService.getDropdownOptions().subscribe({
      next: (options) => {
        this.locations = options.locations;
        this.departments = options.departments;
        this.positions = options.positions;
      }
    });
  }

  save(): void {
    if (!this.profile) return;
    const userId = this.profile.id;
    this.saving = true;

    // Construct fullName from firstName and lastName
    const updateRequest = {
      ...this.edit,
      fullName: `${this.edit.firstName?.trim() || ''} ${this.edit.lastName?.trim() || ''}`.trim()
    };

    this.userProfileService.updateProfile(userId, updateRequest).subscribe({
      next: (updated) => {
        this.profile = { ...updated } as UserProfile;
        this.notifications.success('Profile Updated', 'Your profile has been updated successfully.');
      },
      error: (error: any) => {
        if (error?.status !== 403) {
          this.notifications.error('Update Failed', 'Could not update profile.');
        }
      },
      complete: () => (this.saving = false)
    });
  }

  reset(): void {
    if (!this.profile) return;
    this.edit = {
      firstName: this.profile.firstName,
      lastName: this.profile.lastName,
      email: this.profile.email,
      phoneNumber: this.profile.phoneNumber,
      employeeId: this.profile.employeeId,
      avatar: this.profile.avatar,
      department: this.profile.department,
      jobTitle: this.profile.jobTitle,
      location: this.profile.location,
      notes: this.profile.notes,
      preferences: this.profile.preferences
    } as UpdateProfileRequest;
  }

  onAvatarChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      const file = input.files[0];
      this.uploadAvatar(file);
    }
  }

  uploadAvatar(file: File): void {
    if (!this.profile) return;
    
    // Validate file type
    if (!file.type.startsWith('image/')) {
      this.notifications.error('Invalid File', 'Please select an image file.');
      return;
    }

    // Validate file size (5MB limit)
    if (file.size > 5 * 1024 * 1024) {
      this.notifications.error('File Too Large', 'Please select an image smaller than 5MB.');
      return;
    }

    this.uploadingAvatar = true;
    const userId = this.profile.id;

    this.userProfileService.uploadAvatar(userId, file).subscribe({
      next: (response) => {
        this.notifications.success('Avatar Updated', 'Your profile picture has been updated successfully.');
        // Refresh profile data
        this.userProfileService.getProfile(userId).subscribe({
          next: (updatedProfile) => {
            this.profile = updatedProfile;
            this.edit.avatar = updatedProfile.avatar;
            this.uploadingAvatar = false;
            // Dispatch event to update header avatar
            window.dispatchEvent(new CustomEvent('avatar-updated', {
              detail: {
                avatarUrl: updatedProfile.avatarUrl,
                avatar: updatedProfile.avatar
              }
            }));
          },
          error: () => {
            this.uploadingAvatar = false;
          }
        });
      },
      error: (error) => {
        if (error?.status !== 403) {
          this.notifications.error('Upload Failed', 'Could not update profile picture.');
        }
        this.uploadingAvatar = false;
      }
    });
  }

  removeAvatar(): void {
    if (!this.profile) return;

    this.uploadingAvatar = true;
    const userId = this.profile.id;

    this.userProfileService.removeAvatar(userId).subscribe({
      next: (response) => {
        this.notifications.success('Avatar Removed', 'Your profile picture has been removed successfully.');
        // Refresh profile data
        this.userProfileService.getProfile(userId).subscribe({
          next: (updatedProfile) => {
            this.profile = updatedProfile;
            this.edit.avatar = updatedProfile.avatar;
            this.uploadingAvatar = false;
            // Dispatch event to update header avatar
            window.dispatchEvent(new CustomEvent('avatar-updated', {
              detail: {
                avatarUrl: updatedProfile.avatarUrl,
                avatar: updatedProfile.avatar
              }
            }));
          },
          error: () => {
            this.uploadingAvatar = false;
          }
        });
      },
      error: (error) => {
        if (error?.status !== 403) {
          this.notifications.error('Remove Failed', 'Could not remove profile picture.');
        }
        this.uploadingAvatar = false;
      }
    });
  }

  getInitials(): string {
    if (!this.profile) return '?';

    const firstInitial = this.profile.firstName?.charAt(0)?.toUpperCase() || '';
    const lastInitial = this.profile.lastName?.charAt(0)?.toUpperCase() || '';

    return firstInitial + lastInitial || '?';
  }

  onImageError(event: Event): void {
    // If image fails to load, fall back to default avatar
    const target = event.target as HTMLImageElement;
    if (this.profile && this.profile.avatar) {
      // Set avatar to empty so it falls back to default avatar
      this.profile.avatar = '';
    }
  }

}


