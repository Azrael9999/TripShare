import { createRouter, createWebHistory } from 'vue-router'
import Home from '../views/Home.vue'
import TripDetails from '../views/TripDetails.vue'
import CreateTrip from '../views/CreateTrip.vue'
import MyBookings from '../views/MyBookings.vue'
import Profile from '../views/Profile.vue'
import VerifyEmail from '../views/VerifyEmail.vue'
import Notifications from '../views/Notifications.vue'
import Vehicle from '../views/Vehicle.vue'
import Safety from '../views/Safety.vue'
import MyTrips from '../views/MyTrips.vue'
import Messages from '../views/Messages.vue'
import AdminDashboard from '../views/admin/AdminDashboard.vue'
import AdminReports from '../views/admin/AdminReports.vue'
import AdminIdentity from '../views/admin/AdminIdentity.vue'
import AdminAds from '../views/admin/AdminAds.vue'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Home, meta: { title: 'TripShare Sri Lanka | Carpool & Ride Sharing' } },
    { path: '/trips/:id', component: TripDetails, meta: { title: 'Trip details | TripShare Sri Lanka' } },
    { path: '/create', component: CreateTrip, meta: { requiresAuth: true, requiresVerified: true, title: 'Create trip | TripShare' } },
    { path: '/my-trips', component: MyTrips, meta: { requiresAuth: true, requiresVerified: true, title: 'My trips | TripShare' } },
    { path: '/bookings', component: MyBookings, meta: { requiresAuth: true, requiresVerified: true, title: 'My bookings | TripShare' } },
    { path: '/notifications', component: Notifications, meta: { requiresAuth: true, title: 'Alerts | TripShare' } },
    { path: '/messages', component: Messages, meta: { requiresAuth: true, title: 'Messages | TripShare' } },
    { path: '/profile', component: Profile, meta: { requiresAuth: true, title: 'Profile | TripShare' } },
    { path: '/vehicle', component: Vehicle, meta: { requiresAuth: true, requiresVerified: true, title: 'Vehicle profile | TripShare' } },
    { path: '/safety', component: Safety, meta: { requiresAuth: true, title: 'Safety | TripShare' } },
    { path: '/verify-email', component: VerifyEmail, meta: { requiresAuth: true, title: 'Verify email | TripShare' } },
    { path: '/admin', component: AdminDashboard, meta: { requiresAuth: true, requiresAdmin: true, title: 'Admin | TripShare' } },
    { path: '/admin/reports', component: AdminReports, meta: { requiresAuth: true, requiresAdmin: true, title: 'Admin reports | TripShare' } },
    { path: '/admin/identity', component: AdminIdentity, meta: { requiresAuth: true, requiresAdmin: true, title: 'Admin identity | TripShare' } },
    { path: '/admin/ads', component: AdminAds, meta: { requiresAuth: true, requiresAdmin: true, title: 'Admin ads | TripShare' } }
  ]
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  if (!auth.initialized) await auth.init()

  if ((to.meta as any)?.requiresAuth && !auth.isAuthenticated) {
    return { path: '/', query: { login: '1' } }
  }

  if ((to.meta as any)?.requiresVerified && auth.isAuthenticated && !auth.me?.emailVerified) {
    return { path: '/profile', query: { verify: '1' } }
  }

  if ((to.meta as any)?.requiresAdmin && auth.isAuthenticated && auth.me?.role !== 'admin') {
    return { path: '/' }
  }
})

export default router
